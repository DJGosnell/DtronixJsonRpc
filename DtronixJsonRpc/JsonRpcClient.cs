﻿using Newtonsoft.Json;
using Newtonsoft.Json.Bson;
using Newtonsoft.Json.Linq;
using NLog;
using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace DtronixJsonRpc {

	/// <summary>
	/// Client to connect and communicate on the JSON RPC protocol.
	/// </summary>
	/// <typeparam name="THandler">Action Handler to contain all action class instances.</typeparam>
	public class JsonRpcClient<THandler> : IDisposable
		where THandler : ActionHandler<THandler>, new() {

		private class ReturnResult {
			public ManualResetEventSlim reset_event;
			public JToken value;
		}

		private static Logger logger = LogManager.GetCurrentClassLogger();

		/// <summary>
		/// Event called when this client disconnects or is forcibly disconnected from the server.
		/// </summary>
		public event EventHandler<JsonRpcClient<THandler>, ClientDisconnectEventArgs<THandler>> OnDisconnect;

		/// <summary>
		/// Event called when this client has successfully connected and authenticated on the server.
		/// </summary>
		public event EventHandler<JsonRpcClient<THandler>, ClientConnectEventArgs<THandler>> OnConnect;

		/// <summary>
		/// Event called when this client has started the authentication process.
		/// </summary>
		/// <remarks>
		/// The data property has a max length of 2048 characters.
		/// The Data property is the raw data that the server verifies.
		/// If the client succeeds in the challenge, set the Authenticated property to true.
		/// If the client fails in the challenge, set the Authenticated property to false and set the FailureReason property to the reason the authentication failed.
		/// </remarks>
		public event EventHandler<JsonRpcClient<THandler>, ConnectorAuthenticationEventArgs> OnAuthenticationRequest;

		/// <summary>
		/// Event called when the client fails authentication.  Provides the reason for the failure the Reason property.
		/// </summary>
		public event EventHandler<JsonRpcClient<THandler>, AuthenticationFailureEventArgs> OnAuthenticationFailure;

		/// <summary>
		/// Event called when the receives information about the other end of the connection.
		/// </summary>
		public event EventHandler<JsonRpcClient<THandler>, ReceiveConnectionInformationEventArgs> OnReceiveConnectionInformation;

		/// <summary>
		/// Event called when a client has changed status. (Connected, Disconnected).
		/// May not be called if the server is not configured to broadcast changes.
		/// </summary>
		public event EventHandler<JsonRpcClient<THandler>, ConnectedClientChangedEventArgs> OnConnectedClientChange;

		/// <summary>
		/// Event called by the server to verify the client's authentication challenge.
		/// </summary>
		internal event EventHandler<JsonRpcClient<THandler>, ConnectorAuthenticationEventArgs> OnAuthenticationVerify;

		/// <summary>
		/// Event called by the connector when it has received data from the other party.
		/// </summary>
		internal event EventHandler<JsonRpcClient<THandler>, OnDataReceivedEventArgs> OnDataReceived;

		/// <summary>
		/// Mode this client is set to.
		/// </summary>
		/// <remarks>
		/// If set to Server, this client connector is the connector that the server uses to communicate with the client.
		/// If set to Client, this client is used as the connector to communicate with the server.
		/// </remarks>
		public JsonRpcSource Mode { get; private set; }

		/// <summary>
		/// If the "client" is running in Server mode, this will contain the instance of the running server. Otherwise, if the client is in Client mode, this is null.
		/// </summary>
		public JsonRpcServer<THandler> Server { get; private set; }

		/// <summary>
		/// The time that an action will wait for a response from the server.
		/// </summary>
		public int RequestTimeout { get; set; } = 30 * 1000;

		/// <summary>
		/// The number of milliseconds elapsed to execute a command from the client to the server.
		/// </summary>
		public long Ping { get; private set; } = -1;

		/// <summary>
		/// Connected client information
		/// </summary>
		public ClientInfo Info { get; private set; } = new ClientInfo();

		/// <summary>
		/// Address this client is connecting from.
		/// </summary>
		public string Address { get; private set; }

		/// <summary>
		/// Port this client is bound to.
		/// 
		/// Default: 2828.
		/// </summary>
		public int Port { get; private set; } = 2828;

		/// <summary>
		/// Actions to execute on the connected client or server.
		/// </summary>
		public THandler Actions { get; }

		/// <summary>
		/// Object that is referenced by the action handlers.
		/// </summary>
		public object DataObject { get; set; }

		private long _RequestId = 0;

		/// <summary>
		/// Id number for sending requests to the server to allow for requests to be matched up to their responses.
		/// </summary>
		public long RequestId {
			get { return _RequestId; }
		}

		private readonly CancellationTokenSource cancellation_token_source = new CancellationTokenSource();
		// All streams and stream wrappers used in this client.
		private TcpClient client;
		private NetworkStream base_stream;
		private JsonReader reader;
		private JsonWriter writer;
		private StreamReader stream_reader;
		private StreamWriter stream_writer;
		private JsonSerializer serializer;
		private object write_lock = new object();
		private JsonRpcServerConfigurations.TransportMode transport_protocol;
		private ConcurrentDictionary<string, ReturnResult> return_wait_results = new ConcurrentDictionary<string, ReturnResult>();




		internal Stopwatch ping_stopwatch;

		private const int AUTH_TIMEOUT = 2000;

		/// <summary>
		/// Creates a new Client to connect to  and communicate with a JSON RPC server.
		/// </summary>
		/// <param name="address">Address the server is located.</param>
		/// <param name="port">Port the server is bound to.</param>
		/// <param name="transport_protocol">Transport protocol that this client will communicate in. Default: BSON mode.</param>
		/// <returns>New configured instance of a JsonRpcClient.</returns>
		public static JsonRpcClient<THandler> CreateClient(string address, int port = 2828, JsonRpcServerConfigurations.TransportMode transport_protocol = JsonRpcServerConfigurations.TransportMode.Bson) {
			return new JsonRpcClient<THandler>(-1) {
				client = new TcpClient(),
				Mode = JsonRpcSource.Client,
				Port = port,
				Address = address,
				transport_protocol = transport_protocol
			};
		}

		/// <summary>
		/// Creates a new client connector to connect and communicate with another client.
		/// </summary>
		/// <param name="server">Server instance object.</param>
		/// <param name="client">Client retrieved from the TcpListener.</param>
		/// <param name="id">Id of the client provided by the server.</param>
		/// <returns></returns>
		internal static JsonRpcClient<THandler> CreateConnector(JsonRpcServer<THandler> server, TcpClient client, int id) {
			client.SendTimeout = server.Configurations.ClientSendTimeout;

			return new JsonRpcClient<THandler>(id) {
				Server = server,
				client = client,
				Mode = JsonRpcSource.Server,
				Port = server.Configurations.BindingPort,
				Address = ((IPEndPoint)client.Client.RemoteEndPoint).Address.ToString(),
				ping_stopwatch = new Stopwatch(),
				transport_protocol = server.Configurations.TransportProtocol
			};
		}

		/// <summary>
		/// Creates a new instance of the JsonRpcClient class.
		/// </summary>
		/// <param name="id">ID of the client connecting. Set to -1 if the client ID is not known.</param>
		/// <remarks>
		/// Use of this is not directly allowed because configuration is required before full construction can occur.
		/// </remarks>
		private JsonRpcClient(int id) {
			Info.Id = id;
			Actions = new THandler();
			Actions.Connector = this;
			serializer = new JsonSerializer();
			Info.Version = Actions.Version;
		}

		/// <summary>
		/// Increments to the next request ID and returns it as a string.
		/// </summary>
		/// <returns>String representation of the request ID.</returns>
		public string GetNewRequestId() {
			return Interlocked.Increment(ref _RequestId).ToString();
		}

		/// <summary>
		/// Asynchronously waits for the result of the specified ID with an optional cancellation token.  Can only have one wait per ID.
		/// </summary>
		/// <typeparam name="T">Type to return the result as.</typeparam>
		/// <param name="id">ID of the request to wait on.</param
		/// <param name="token">Cancellation token for the awaiter.</param>
		/// <returns>Result of the request.</returns>
		public async Task<T> WaitForResult<T>(string id, CancellationToken token = default(CancellationToken)) {
			ReturnResult result;

			// If a cancellation token is not provided, use the one for the client.
			if (token == default(CancellationToken)) {
				token = cancellation_token_source.Token;
			}

			try {
				if (return_wait_results.TryGetValue(id, out result) == false) {
					logger.Fatal("{0} CID {1}: Could not get wait for request ID '{2}'.", Mode, Info.Id, id);
				}

				logger.Trace("{0} CID {1}: Waiting for result from request ID '{2}'.", Mode, Info.Id, id);

				// Set the wait inside a task and set the timeouts.
				if (await Task.Run(() => result.reset_event.Wait(RequestTimeout, token), token) == false) {
					throw new TimeoutException("Connector took too long to respond to request.");
				}

				logger.Trace("{0} CID {1}: Result received for request ID '{2}'.", Mode, Info.Id, id);

				return result.value["result"].ToObject<T>();
			} catch (OperationCanceledException e) {
				logger.Debug("{0} CID {1}: Request ID '{2}' has been requested to be canceled on the remote connection.", Mode, Info.Id, id);
				Send(new JsonRpcRequest("rpc.cancel-action", id));
				throw e;

			} finally {
				if (return_wait_results.TryRemove(id, out result) == false) {
					logger.Fatal("{0} CID {1}: Could not remove the wait for request ID '{2}'.", Mode, Info.Id, id);
				}
			}
		}

		/// <summary>
		/// Method to provide authentication verification from the server side.
		/// </summary>
		/// <returns>Returns on successful authentication. False otherwise.</returns>
		private ClientConnectionResponse AuthenticateClient() {
			try {
				// Read the client info object.
				var client_conn_info = Read()?["params"]?.ToObject<ClientConnectionRequest>();

				// Check to ensure a valid connection request class was passed.
				if (client_conn_info == null) {
					return new ClientConnectionResponse("User information passed was invalid.");
				}

				// If the server requires the same version client, verify it.
				if (Server.Configurations.RequireSameVersion && client_conn_info.Version != Actions.Version) {
					return new ClientConnectionResponse("Client is not the same version as the server.");
				}

				// Clean the username.
				Info.Username = client_conn_info.Username?.Trim();

				// Check to see if a username was provided.
				if (Server.Configurations.AllowAnonymousConnections == false && string.IsNullOrEmpty(Info.Username)) {
					return new ClientConnectionResponse("Did not provide a username.");
				}

				// Check to ensure the client should connect.
				if (Info.Username != null &&
					Server.Configurations.AllowDuplicateUsernames == false &&
					Server.Clients.Values.Any(cli => cli.Info.Id != Info.Id && cli.Info.Username == Info.Username)) {
					return new ClientConnectionResponse("Client connected with duplicate username.");
				}

				// If the client connecting has an empty username, it is an anonymous client and will only receive information about the server.
				if (string.IsNullOrEmpty(Info.Username)) {
					logger.Debug("{0} CID {1}: Connected in anonymous mode.", Mode, Info.Id);
					return new ClientConnectionResponse() {
						AnonymousClient = true
					};
				}

				// Authorize client.
				var auth_args = new ConnectorAuthenticationEventArgs() {
					Data = client_conn_info.AuthData
				};

				// Verify the client against the event set.
				if (OnAuthenticationVerify != null) {

					// Fire the event to verify that the client has the proper data.
					OnAuthenticationVerify?.Invoke(this, auth_args);

					// Verify whether or not the client has passed the challenge.
					if (auth_args.Authenticated == false) {
						return new ClientConnectionResponse(auth_args.FailureReason);
					}
				}

			} catch (OperationCanceledException) {

				// Called if the authentication timeout has occurred.
				return new ClientConnectionResponse("Client authentication timeout.");
			}

			logger.Debug("{0} CID {1}: Successfully authenticated on the server.", Mode, Info.Id);

			return new ClientConnectionResponse() {
				ClientId = Info.Id
			};
		}

		/// <summary>
		/// Requests authentication verification from the client side.
		/// </summary>
		private ClientConnectionResponse RequestAuthentication() {
			try {
				var auth_args = new ConnectorAuthenticationEventArgs();

				// Fire the event to set the challenge data before sending it to the server.
				OnAuthenticationRequest?.Invoke(this, auth_args);

				// Setup the connection request information
				var conn_request = new ClientConnectionRequest() {
					Username = Info.Username,
					Version = Actions.Version,
					AuthData = auth_args.Data
				};

				// Send this client info to the server.
				Send(new JsonRpcRequest(null, conn_request), true);

				// Read the response from the server
				var vs = Read();
				var response = vs?["params"]?.ToObject<ClientConnectionResponse>();

				// If the response is null, the client did not receive valid data from the server.
				if (response == null) {
					return new ClientConnectionResponse("Server did not return valid data.");
				}

				// If an error occurred, let the client know.
				if (response.Error != null) {
					return response;
				}

				Info.Id = response.ClientId;

				return response;

			} catch (OperationCanceledException) {
				return new ClientConnectionResponse("Server authentication timed out.");
			}
		}

		/// <summary>
		/// Connects to the server with the specified connection info.
		/// </summary>
		/// <returns>
		/// True on successful client connection, false if client did not fully connect. (Anonymous client, failed authentication, etc...)
		/// </returns>
		public bool Connect() {
			ClientConnectionResponse result;


			logger.Info("{0} CID {1}: New client started", Mode, Info.Id);

			// Set the client into "Connecting" mode. This prevents normal requests from executing.
			Info.Status = ClientStatus.Connecting;

			// If we are in client mode, we need to initiate the connection.  If we are in Server mode, the connection is already active.
			if (Mode == JsonRpcSource.Client) {

				// Reset the client to the default settings for a client.
				Info.DisconnectReason = null;
				Info.Id = -1;

				logger.Info("{0} CID {1}: Attempting to connect to server", Mode, Info.Id);

				// Attempt to connect to the server.  If we do not connect in the timeout period or a cancellation has been requested, cancel the connection attempt.
				var completed = client.ConnectAsync(Address, Port).Wait(3000, cancellation_token_source.Token);

				if (completed == false && client.Connected == false) {
					logger.Warn("{0} CID {1}: Attempted connection did not complete successfully.", Mode, Info.Id);
					Disconnect("Could not connect client in a reasonable amount of time.", JsonRpcSource.Client, SocketError.TimedOut);
					return false;
				}
			}

			base_stream = client.GetStream();

			// Determine the transport mode of this client.
			if (transport_protocol == JsonRpcServerConfigurations.TransportMode.Bson) {
				writer = new BsonWriter(base_stream);
				reader = new BsonReader(base_stream);

			} else if (transport_protocol == JsonRpcServerConfigurations.TransportMode.Json) {
				// We have to use stream readers and writers because the JSON text writer/reader has to have a text reader/writer.
				stream_writer = new StreamWriter(base_stream, Encoding.UTF8, 1024 * 16, true);
				stream_reader = new StreamReader(base_stream, Encoding.UTF8, true, 1024 * 16, true);

				writer = new JsonTextWriter(stream_writer);
				reader = new JsonTextReader(stream_reader);
			}

			// Don't indent the transport data
			writer.Formatting = Formatting.None;

			// Prevent the decoder from throwing when it realizes there is more than one data context in the stream.
			reader.SupportMultipleContent = true;

			logger.Debug("{0} CID {1}: Connected. Authenticating...", Mode, Info.Id);

			// If the client has not authenticated withing the limit, kick it.
			Task.Delay(AUTH_TIMEOUT).ContinueWith(task => {
				if (Info.Status == ClientStatus.Connecting) {
					Disconnect("Client did not authenticate within time limitation.");
				}
			});

			if (Mode == JsonRpcSource.Server) {

				// If we are the server, verify the authentication data passed.
				result = AuthenticateClient();

				// Set the server information
				result.ServerName = Server.Configurations.ServerName;
				result.ServerData = Server.Configurations.ServerData;
				result.Version = Actions.Version;

				// Let the client know the result.
				Send(new JsonRpcRequest(null, result), true);

			} else {

				// Request verification of the authentication data.
				result = RequestAuthentication();

				// Invoke the event to let the client know the information about the server.
				if (OnReceiveConnectionInformation != null) {
					var args = new ReceiveConnectionInformationEventArgs() {
						Version = result.Version,
						ServerData = result.ServerData,
						ServerName = result.ServerName
					};

					OnReceiveConnectionInformation.Invoke(this, args);
				}
			}

			// Check for errors and handle them if they occurred.
			if (result.Error != null) {
				logger.Debug("{0} CID {1}: Authentication failure. Reason: {2}", Mode, Info.Id, result.Error);

				// Fire the failure event.
				OnAuthenticationFailure?.Invoke(this, new AuthenticationFailureEventArgs(result.Error));

				// Disconnect the client
				Disconnect(result.Error);
				return false;
			}

			// If this is an anonymous connection, disconnect at this point.
			if (result.AnonymousClient) {
				Disconnect("Disconnecting anonymous connection.");
				return false;
			}

			logger.Debug("{0} CID {1}: Authentication success.", Mode, Info.Id);

			// Client has successfully authenticated.
			Info.Status = ClientStatus.Connected;

			// Fire the connection event.
			OnConnect?.Invoke(this, new ClientConnectEventArgs<THandler>(Server, this));

			// Inform all the other clients of the new connection if desired.
			if (Mode == JsonRpcSource.Server && Server.Configurations.BroadcastClientStatusChanges) {

				// Broadcast to all the other clients that there is a new connection if desired.
				Server.EachClient(cl => {
					cl.Send(new JsonRpcRequest("rpc." + nameof(OnConnectedClientChange), new ClientInfo[] { Info }));
				});

				// Inform this client that it has connected and send it all the connected clients.
				Send(new JsonRpcRequest("rpc." + nameof(OnConnectedClientChange), Server.Clients.Select(cl => cl.Value.Info).ToArray()), true);
			}

			// Read over the incoming data until we have a cancellation request in a new task.
			Task.Factory.StartNew(ListenerLoop, TaskCreationOptions.LongRunning, cancellation_token_source.Token);

			return true;
		}

		/// <summary>
		/// Loop used for listening for received commands.
		/// </summary>
		/// <param name="state">State passed to this method from the new thread.</param>
		private void ListenerLoop(object state) {
			try {
				while (cancellation_token_source.IsCancellationRequested == false) {

					// Raw incoming data from the other end of the connection.
					JToken data;

					// Read synchronously.  Will wait until data is read from the stream or the underlying stream is canceled.
					data = Read();

					// Call the internal event.
					var data_received_event_args = new OnDataReceivedEventArgs(data);
					OnDataReceived?.Invoke(this, data_received_event_args);

					//If the call was handled in the event, continue on to the next request.
					if (data_received_event_args.Handled) {
						logger.Debug("{0} CID {1}: Request by the other client was handled by the event handler.", Mode, Info.Id);
						continue;
					}

					// See if we have reached the end of the stream.
					if (data == null) {
						return;
					}
					// Determine if we have and ID.
					string id = data["id"]?.ToObject<string>();

					// If we have an ID and we have a wait handle, then we have and existing method to return a value to.
					if (id != null && return_wait_results.Count != 0) {
						ReturnResult return_result;
						if (return_wait_results.TryGetValue(id, out return_result)) {
							return_result.value = data;

							// Let the other thread know it is OK to continue.
							return_result.reset_event.Set();

							// Continue on to the next request.
							continue;
						}
					}

					// Set the ID to String.Empty to allow the notifications to be executed on this connection.
					if (id == null) {
						id = string.Empty;
					}

					// Get the called method once and determine what to do with the data.
					string method = data["method"].Value<string>();

					logger.Debug("{0} CID {1}: Method '{2}' called", Mode, Info.Id, method);

					// If the connection requested an invalid method, do nothing with it.
					// TODO: Maybe come up with a hit system when the client does too many invalid actions, it will get kicked.
					if (string.IsNullOrWhiteSpace(method)) {
						continue;
					}

					try {
						// If the method starts with a "rpc", the method is a special method and handled internally.
						if (method.StartsWith("rpc.")) {
							ExecuteExtentionAction(method, data);
						} else {
							Actions.ExecuteAction(method, data, id);
						}

					} catch (OperationCanceledException e) {
						string s = "";
					} catch (Exception e) {
						logger.Error(e, "{0} CID {1}: Called action threw exception. Exception: {2}", Mode, Info.Id, e.ToString());
						throw e;
					}

				}

			} catch (SocketException e) {
				if (Info.Status.HasFlag(ClientStatus.Connected)) {
					Disconnect("Server connection issues", JsonRpcSource.Client, e.SocketErrorCode);
				}

			} catch (Exception e) {
				logger.Error(e, "{0} CID {1}: Exception Occurred: {2}", Mode, Info.Id, e.ToString());
				throw e;

			} finally {
				if (Info.Status != ClientStatus.Disconnected && Info.Status != ClientStatus.Disconnecting) {
					Disconnect("Client closed");
				}
			}
		}


		/// <summary>
		/// Executes an extension action with the data passed.
		/// </summary>
		/// <param name="method">The request method name</param>
		/// <param name="data">JData to parse and handle.</param>
		private void ExecuteExtentionAction(string method, JToken data) {

			if (method == "rpc." + nameof(OnConnectedClientChange)) {

				// Method called to alert this client of changes to other clients on the server.
				// Optionally called depending on the server configurations.
				if (Mode == JsonRpcSource.Client) {
					var clients_info = data["params"].ToObject<ClientInfo[]>();
					OnConnectedClientChange?.Invoke(this, new ConnectedClientChangedEventArgs(clients_info));
				}

			} else if (method == "rpc." + nameof(OnDisconnect)) {

				// Method called when disconnected by the other end of the connection.
				var clients_info = data["params"].ToObject<ClientInfo[]>();
				if (Info.Status.HasFlag(ClientStatus.Connected)) {
					Disconnect(clients_info[0].DisconnectReason, (Mode == JsonRpcSource.Client) ? JsonRpcSource.Server : JsonRpcSource.Client, SocketError.Success);
				}

			} else if (method == "rpc.cancel-action") {

				// Method called to cancel an actively running action on this connector.
				var id = data["params"].ToObject<string>();

				logger.Debug("{0} CID {1}: Request ID '{2}' has been requested to be canceled.", Mode, Info.Id, id);

				CancellationTokenSource source;
				if (Actions.active_cancellable_actions.TryGetValue(id, out source)) {
					logger.Debug("{0} CID {1}: Request ID '{2}' canceled.", Mode, Info.Id, id);
					source.Cancel();
				} else {
					logger.Debug("{0} CID {1}: Request ID '{2}' not found.", Mode, Info.Id, id);
				}

			} else if (method == "rpc.ping") {

				// Method called to determine the latency of the connection.
				// Server pings client, client responds immediately, server sends the latency back to the client.
				if (Mode == JsonRpcSource.Server) {
					ping_stopwatch.Stop();
					Ping = ping_stopwatch.ElapsedMilliseconds;
					Send(new JsonRpcRequest("rpc.ping-result", Ping));

					logger.Trace("{0} CID {1}: Ping {2}ms", Mode, Info.Id, Ping);

				} else {
					// Ping back immediately to the server.
					Send(new JsonRpcRequest("rpc.ping", Mode));
				}

			} else if (method == "rpc.ping-result") {

				// Method called on the client to update the latency value from the server.
				if (Mode == JsonRpcSource.Client) {
					Ping = data["params"].ToObject<long>();
				}

			} else {
				if (Info.Status.HasFlag(ClientStatus.Connected)) {
					Disconnect("Tired to call invalid method. Check client version.");
				}
			}

		}

		/// <summary>
		/// Reads the next JSON object from the stream with the defined transport protocol.
		/// </summary>
		/// <returns>Raw data object from the stream.</returns>
		private JToken Read() {
			JToken data;
			try {
				// Move the head to the next token in the stream.
				reader.Read();

				// Read the entire object from the stream forward.  Stops at the end of this object.
				data = JToken.ReadFrom(reader);

			} catch (OperationCanceledException e) {
				logger.Debug(e, "{0} CID {1}: Method reader was canceled", Mode, Info.Id);
				return null;

			} catch (SocketException e) {
				logger.Warn(e, "{0} CID {1}: Socket Exception occurred while listening. Exception: {2}", Mode, Info.Id, e.InnerException.ToString());
				if (Info.Status.HasFlag(ClientStatus.Connected)) {
					Disconnect("Connection closed");
				}
				return null;

			} catch (IOException e) {

				logger.Warn(e, "{0} CID {1}: IO Exception occurred while listening. Exception: {2}", Mode, Info.Id, e.InnerException.ToString());
				if (Info.Status.HasFlag(ClientStatus.Connected)) {
					Disconnect("Connection closed");
				}
				return null;

			} catch (JsonReaderException e) {
				logger.Warn(e, "{0} CID {1}: JSON parsing Exception occurred. Exception: {2}", Mode, Info.Id, e.ToString());
				if (Info.Status.HasFlag(ClientStatus.Connected)) {
					Disconnect("Connection closed");
				}
				return null;

			} catch (Exception e) {
				logger.Error(e, "{0} CID {1}: Unknown exception occurred while listening. Exception: {2}", Mode, Info.Id, e.ToString());
				if (Info.Status.HasFlag(ClientStatus.Connected)) {
					Disconnect("Connection closed");
				}
				return null;
			}

			logger.Trace("{0} CID {1}: Read object from stream: {2}", Mode, Info.Id, data.ToString(Formatting.None));

			return data;
		}

		/// <summary>
		/// Sends data to the stream with the specified transport protocol.
		/// </summary>
		/// <typeparam name="T">Data type that will be sent.</typeparam>
		/// <param name="parameters">Data to send over the stream.</param>
		public void Send(JsonRpcRequest parameters) {
			Send(parameters, false);
		}

		/// <summary>
		/// Sends data to the stream with the specified transport protocol.
		/// </summary>
		/// <typeparam name="T">Data type that will be sent.</typeparam>
		/// <param name="parameters">Data to send over the stream.</param>
		/// <param name="force_send">Set to true to ignore connection status. Otherwise, will throw if data is sent over a connecting connection.</param>
		private void Send(JsonRpcRequest parameters, bool force_send = false) {

			// Prevent data from being sent when the client has been requested to stop.
			if (cancellation_token_source.IsCancellationRequested) {
				return;
			}

			// Ensure that the underlying stream is actually connected.
			if (client.Client.Connected == false) {
				return;
			}

			// Prevent the client from sending data when the client is connected unless overridden.
			if (Info.Status == ClientStatus.Connecting && force_send == false) {
				throw new InvalidOperationException("Can not send request while still connecting.");
			}

			// MAJOR Performance Hit.
			logger.Trace("{0} CID {1}: Write line to stream: {2}", Mode, Info.Id, JsonConvert.SerializeObject(parameters));


			// Add the reset event if the arguments have an ID.
			if (parameters.Id != null) {
				logger.Trace("{0} CID {1}: Waiting on request result for request ID '{2}'.", Mode, Info.Id, parameters.Id);


				var result_instance = new ReturnResult() { reset_event = new ManualResetEventSlim() };
				// Add it to the list
				if (return_wait_results.TryAdd(parameters.Id, result_instance) == false) {
					logger.Fatal("{0} CID {1}: Could not add wait request for request ID '{2}'.", Mode, Info.Id, parameters.Id);
				}
			}

			try {
				// Lock the stream so that only one writer can execute at a time.
				lock (write_lock) {

					// Write the data to the stream with the specified transport protocol.
					serializer.Serialize(writer, parameters);
					writer.Flush();
				}

			} catch (IOException e) {
				if (client.Client.Connected) {
					logger.Error(e, "{0} CID {1}: Exception occurred when trying to write to the stream. Exception: {2}", Mode, Info.Id, e.ToString());
				}

				if (Info.Status.HasFlag(ClientStatus.Connected)) {
					Disconnect("Writing error to stream.");
				}

			} catch (ObjectDisposedException e) {
				logger.Warn("{0} CID {1}: Tried to write to the stream when the client was closed.", Mode, Info.Id);
				// The client was closed.  
				return;

			} catch (OperationCanceledException) {
				return; // No need to log or do anything because the connection was just requested to be closed.

			} catch (Exception e) {
				logger.Error(e, "{0} CID {1}: Unknown error occurred while writing to the stream. Exception: {2}", Mode, Info.Id, e.ToString());
				throw e;
			}
		}

		/// <summary>
		/// Stops the connection and alerts the other party of this fact.
		/// </summary>
		/// <param name="reason">Reason this client is disconnecting.</param>
		public void Disconnect(string reason) {
			Disconnect(reason, Mode, SocketError.Success);
		}


		/// <summary>
		/// Stops the connection and alerts the other party of this face.
		/// </summary>
		/// <param name="reason">Reason this client is disconnecting.</param>
		/// <param name="source">Source of this disconnect request</param>
		/// <param name="socket_error"></param>
		private void Disconnect(string reason, JsonRpcSource source, SocketError socket_error) {
			if (string.IsNullOrWhiteSpace(reason)) {
				throw new ArgumentException("Reason for closing connection can not be null or empty.");
			}

			logger.Info("{0} CID {1}: Stop requested. Reason: {2}", Mode, Info.Id, reason);

			// If the client is disconnecting already, do nothing.
			if (Info.Status == ClientStatus.Disconnecting) {
				logger.Debug("{0} CID {1}: Stop requested but client is already in the process of stopping.", Mode, Info.Id);
				return;
			} else {
				// Set the current status
				Info.Status = ClientStatus.Disconnecting;
			}

			Info.DisconnectReason = reason;

			// If we are disconnecting, let the other party know.
			if (Mode == source && client.Client.Connected) {
				Send(new JsonRpcRequest("rpc." + nameof(OnDisconnect), new ClientInfo[] { Info }));
			}

			// Invoke the disconnect event.
			OnDisconnect?.Invoke(this, new ClientDisconnectEventArgs<THandler>(reason, source, Server, this, socket_error));
			OnDisconnect = null;
			cancellation_token_source?.Cancel();


			// Close and dispose all streams.
			client?.Close();
			base_stream?.Dispose();
			stream_writer?.Dispose();
			stream_reader?.Dispose();
			writer?.Close();
			reader?.Close();

			// Stop the stopwatch for the ping.
			ping_stopwatch?.Stop();

			// Set that this client has finally disconnected.
			Info.Status = ClientStatus.Disconnected;
			logger.Info("{0} CID {1}: Stopped", Mode, Info.Id);
		}

		/// <summary>
		/// Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.
		/// </summary>
		public void Dispose() {
			Disconnect("Class object disposed.");
		}
	}
}

