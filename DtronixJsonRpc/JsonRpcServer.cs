using NLog;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace DtronixJsonRpc {
    public class JsonRpcServer<THandler> : IDisposable
		where THandler : ActionHandler<THandler>, new() {

		private static Logger logger = LogManager.GetCurrentClassLogger();

		public event EventHandler<JsonRpcConnector<THandler>, ConnectorAuthenticationEventArgs> OnAuthenticationVerification;

		public event EventHandler<JsonRpcServer<THandler>> OnStop;
		public event EventHandler<JsonRpcServer<THandler>> OnStart;
		public event EventHandler<JsonRpcServer<THandler>, ClientConnectEventArgs<THandler>> OnClientConnect;
		public event EventHandler<JsonRpcServer<THandler>, ClientDisconnectEventArgs<THandler>> OnClientDisconnect;

		private readonly CancellationTokenSource cancellation_token_source;
        private readonly TcpListener listener;

        public bool IsRunning { get; set; }

        private ConcurrentDictionary<int, JsonRpcConnector<THandler>> clients = new ConcurrentDictionary<int, JsonRpcConnector<THandler>>();

		/// <summary>
		/// Object that is referenced by the action handlers.
		/// </summary>
		public object DataObject { get; set; }

		public ConcurrentDictionary<int, JsonRpcConnector<THandler>> Clients {
            get {
                return clients;
            }
        }

        private int last_client_id = 0;

		private bool _IsStopping = false;

		public bool IsStopping {
			get { return _IsStopping; }
		}

		public JsonRpcServer() : this(IPAddress.Any, 2828) {
		}

		public JsonRpcServer(int port) : this(IPAddress.Any, port) {
		}


		public JsonRpcServer(IPAddress address, int port) {
            cancellation_token_source = new CancellationTokenSource();
            listener = new TcpListener(address, port);
        }


        public void Start() {
			try {

				logger.Info("Server: Starting");
				listener.Start();
			} catch (Exception) {
				throw;
			}

			logger.Debug("Server: Listening for connections.");
            IsRunning = true;
            OnStart?.Invoke(this, this);
			while (cancellation_token_source.IsCancellationRequested == false) {
				var client = listener.AcceptTcpClientAsync();
				try {
					client.Wait(cancellation_token_source.Token);
                    logger.Debug("Server: New client attempting to connect");
                } catch (OperationCanceledException e) {
					logger.Info(e, "Server: Stopped listening for clients.", null);
                    break;

				} catch (Exception e) {
					logger.Error(e, "Server: Unknown exception occured while listening for clients..", null);
                    break;
                }

				var client_listener = new JsonRpcConnector<THandler>(this, client.Result, last_client_id++);

				client_listener.OnDisconnect += (sender, e) => {
					logger.Info("Server: Client ({0}) disconnected. Reason: {1}. Removing from list of active clients.", sender.Info.Id, e.Reason);
					JsonRpcConnector<THandler> removed_client;
					clients.TryRemove(sender.Info.Id, out removed_client);

					Broadcast(cl => cl.Send("$" + nameof(JsonRpcConnector<THandler>.OnConnectedClientChange), new ClientInfo[] { removed_client.Info }));

					OnClientDisconnect?.Invoke(this, e);
				};

				client_listener.OnConnect += (sender, e) => {
					logger.Info("Server: Client ({0}) Connected with username {1}", sender.Info.Id, e.Client.Info.Username);

					OnClientConnect?.Invoke(this, e);
				};

				if (OnAuthenticationVerification != null) {
					client_listener.OnAuthorizationVerify += (sender, e) => {
						OnAuthenticationVerification?.Invoke(sender, e);
					};
				}

				clients.TryAdd(client_listener.Info.Id, client_listener);

                Task.Factory.StartNew(() => client_listener.Connect(), TaskCreationOptions.LongRunning);

            }

			if (cancellation_token_source.IsCancellationRequested) {
				Broadcast(cl => cl.Send("$" + nameof(JsonRpcConnector<THandler>.OnDisconnect), "Server shutting down."));
			}

			logger.Info("Server: Stopped");
        }


        public void Broadcast(Action<JsonRpcConnector<THandler>> action) {
            foreach (var client in clients) {
				if (client.Value.Info.Status == ClientStatus.Connected) {
					logger.Debug("Server: Broadcasting method to client {0}.", client.Value.Info.Id);
					action(client.Value);
				}
            }
        }

        public void Stop(string reason) {
			if (_IsStopping) {
				logger.Debug("Server: Stop requested after server is already in the process of stopping. Reason: {0}", reason);
			}
			_IsStopping = true;

			logger.Info("Server: Stopping server. Reason: {0}", reason);
			Broadcast(cl => {
				if (cl.Info.Status != ClientStatus.Disconnecting) {
					cl.Disconnect("Server Shutdown. Reason: " + reason, JsonRpcSource.Server);
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
