using NLog;
using System;
using System.Collections.Concurrent;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace DtronixJsonRpc {
	/// <summary>
	/// Server to listen and respond via the JSON RPC protocol.
	/// </summary>
	/// <typeparam name="THandler">Action Handler to contain all action class instances.</typeparam>
	public class JsonRpcServer<THandler> : IDisposable
		where THandler : ActionHandler<THandler>, new() {

		private static Logger logger = LogManager.GetCurrentClassLogger();

		/// <summary>
		/// Event called to authenticate the data a client has passed.
		/// </summary>
		/// <remarks>
		/// The data property has a max length of 2048 characters.
		/// The Data property is the raw data that the client has sent to the server.
		/// If the client succeeds in the challenge, set the Authenticated property to true.
		/// If the client fails in the challenge, set the Authenticated property to false and set the FailureReason property to the reason the authentication failed.
		/// </remarks>
		public event EventHandler<JsonRpcClient<THandler>, ConnectorAuthenticationEventArgs> OnAuthenticationVerification;

		/// <summary>
		/// Event called when the server stops listening for clients.
		/// </summary>
		public event EventHandler<JsonRpcServer<THandler>> OnStop;

		/// <summary>
		/// Event called when the server starts listening for clients.
		/// </summary>
		public event EventHandler<JsonRpcServer<THandler>> OnStart;

		/// <summary>
		/// Event called when a new client successfully connects to the server.
		/// </summary>
		public event EventHandler<JsonRpcServer<THandler>, ClientConnectEventArgs<THandler>> OnClientConnect;

		/// <summary>
		/// Event called when the client disconnects. Provides the reason for the disconnect.
		/// </summary>
		public event EventHandler<JsonRpcServer<THandler>, ClientDisconnectEventArgs<THandler>> OnClientDisconnect;

		/// <summary>
		/// Gets the status of whether the server is actively listening for clients or not.
		/// </summary>
		public bool IsRunning { get; private set; }

		/// <summary>
		/// Object that is referenced by the action handlers.
		/// </summary>
		public object DataObject { get; set; }

		/// <summary>
		/// Stored reason why the server was stopped.
		/// </summary>
		public string StopReason { get; private set; }

		/// <summary>
		/// Current configuration of the server.
		/// </summary>
		public JsonRpcServerConfigurations Configurations { get; }

		private ConcurrentDictionary<int, JsonRpcClient<THandler>> clients = new ConcurrentDictionary<int, JsonRpcClient<THandler>>();
		/// <summary>
		/// Gets the dictionary with the active clients and their IDs.
		/// </summary>
		public ConcurrentDictionary<int, JsonRpcClient<THandler>> Clients {
			get {
				return clients;
			}
		}

		private bool _IsStopping = false;
		/// <summary>
		/// Gets whether or not the server is in the process of stopping or not.
		/// </summary>
		public bool IsStopping {
			get { return _IsStopping; }
		}

		private readonly CancellationTokenSource cancellation_token_source;

		/// <summary>
		/// Internal listener for clients.
		/// </summary>
		private readonly TcpListener listener;

		/// <summary>
		/// Last ID used for connected clients.
		/// </summary>
		private int last_client_id = 0;

		private System.Timers.Timer ping_timer;

		/// <summary>
		/// Creates a JsonRpcServer with default configurations.
		/// </summary>
		public JsonRpcServer() : this(new JsonRpcServerConfigurations()) { }


		/// <summary>
		/// Creates a JsonRpcServer instance with the specified configurations
		/// </summary>
		/// <param name="configurations">Configurations to load on server initialization.</param>
		public JsonRpcServer(JsonRpcServerConfigurations configurations) {
			Configurations = configurations;
			cancellation_token_source = new CancellationTokenSource();
			listener = new TcpListener(configurations.BindingAddress, configurations.BindingPort);

			ping_timer = new System.Timers.Timer(Configurations.PingFrequency);

			ping_timer.Elapsed += (sender, e) => {
				EachClient(cl => {
					if (cl.ping_stopwatch.IsRunning) {
						if (cl.ping_stopwatch.ElapsedMilliseconds > configurations.PingTimeoutDisconnectTime) {
							logger.Info("Server: Client ({0}) ping timeout.", cl.Info.Id);
							// If we are still waiting on the ping response, give it until the ping timeout disconnect time.
							cl.Disconnect("Client lost connection to the server. (Ping timeout)");
						}
					} else {
						// If the ping timer is not running, start it.
						cl.ping_stopwatch.Restart();
						cl.Send(new JsonRpcRequest("rpc.ping", JsonRpcSource.Server));
					}
				});
			};
		}


		/// <summary>
		/// Starts listening for incoming clients.  Will listen on the same thread as the start method is called on.
		/// </summary>
		/// <returns>
		/// Returns true on successful start.
		/// </returns>
		public bool Start() {
			logger.Info("Server: Starting");

			// Listens for clients on the bound listener.
			listener.Start();

			logger.Debug("Server: Listening for connections.");
			IsRunning = true;

			// Invoke the event stating the server has started.
			OnStart?.Invoke(this, this);

			// Listen and wait for new incoming connections.
			Task.Factory.StartNew(ListenerLoop, TaskCreationOptions.LongRunning, cancellation_token_source.Token);

			return true;
		}


		private void ListenerLoop(object state) {
			while (cancellation_token_source.IsCancellationRequested == false) {

				// Gets the task for a new client to connect.
				var client = listener.AcceptTcpClientAsync();

				try {
					// Wait synchronously for a new client to connect so that we can cancel.
					client.Wait(cancellation_token_source.Token);
					logger.Debug("Server: New client attempting to connect");

				} catch (OperationCanceledException e) {
					logger.Info(e, "Server: Stopped listening for clients.", null);
					break;

				} catch (Exception e) {
					logger.Error(e, "Server: Unknown exception occurred while listening for clients. Exception: {0}", e.ToString());
					break;
				}

				// Create a new instance of the connector.
				var client_listener = JsonRpcClient<THandler>.CreateConnector(this, client.Result, last_client_id++);

				client_listener.OnDisconnect += (sender, e) => {
					logger.Info("Server: Client ({0}) disconnected. Reason: {1}. Removing from list of active clients.", sender.Info.Id, e.Reason);
					JsonRpcClient<THandler> removed_client;

					// Attempt to remove the client from the list of active clients.
					clients.TryRemove(sender.Info.Id, out removed_client);

					// Stop the ping timer if no clients are connected.
					if (clients.Count == 0) {
						ping_timer.Stop();
					}

					// Alert all the clients that this client disconnected.
					EachClient(cl => cl.Send(new JsonRpcRequest("rpc." + nameof(JsonRpcClient<THandler>.OnConnectedClientChange), new ClientInfo[] { removed_client.Info })));

					// Invoke the event stating that a client disconnected.
					OnClientDisconnect?.Invoke(this, e);
				};

				client_listener.OnConnect += (sender, e) => {
					logger.Info("Server: Client ({0}) Connected with username {1}", sender.Info.Id, e.Client.Info.Username);

					// Invoke the event stating that a new client has successfully connected.
					OnClientConnect?.Invoke(this, e);

					// Start the ping timer if configured so and only if it is not already running.
					if (Configurations.PingClients && ping_timer.Enabled == false) {
						ping_timer.Start();
					}
				};

				// See if we have authentication
				if (OnAuthenticationVerification != null) {
					client_listener.OnAuthenticationVerify += (sender, e) => {

						// Invoke the event to authenticate the connecting client.
						OnAuthenticationVerification?.Invoke(sender, e);
					};
				}

				// Connect and start listening to this client's requests.
				if (client_listener.Connect()) {

					// Add the client to the list of active clients if it connected.
					clients.TryAdd(client_listener.Info.Id, client_listener);
				}

			}

			// If the server is shutting down, broadcast this event to all the clients before a disconnect.
			EachClient(cl => cl.Send(new JsonRpcRequest("rpc." + nameof(JsonRpcClient<THandler>.OnDisconnect), "Server shutting down.")));

			logger.Info("Server: Stopped");
		}
		/// <summary>
		/// Calls an action and passes it a connected client for each client.
		/// </summary>
		/// <param name="action">Action to invoke for each client.</param>
		public void EachClient(Action<JsonRpcClient<THandler>> action) {
			Parallel.ForEach(clients, client => {
				if (client.Value.Info.Status == ClientStatus.Connected) {
					logger.Debug("Server: Broadcasting method to client {0}.", client.Value.Info.Id);
					action(client.Value);
				}
			});
		}

		/// <summary>
		/// Initiates a stop command for the server.
		/// </summary>
		/// <param name="reason">A reason to log and broadcast for the server shutdown.</param>
		public void Stop(string reason) {
			if (StopReason != null) {
				logger.Debug("Server: Stop requested after server is already in the process of stopping. Reason: {0}", reason);
			}

			StopReason = reason;

			logger.Info("Server: Stopping server. Reason: {0}", reason);
			EachClient(cl => {
				if (cl.Info.Status != ClientStatus.Disconnecting) {
					cl.Disconnect("Server Shutdown. Reason: " + reason);
				}
			});

			cancellation_token_source.Cancel();

			listener.Stop();

			IsRunning = false;

			OnStop?.Invoke(this, this);

		}

		public void Dispose() {
			Stop("Class object disposed");
		}


	}
}
