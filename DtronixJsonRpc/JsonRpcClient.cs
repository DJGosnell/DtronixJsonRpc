using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using System.Diagnostics;
using DtronixJsonRpc;
using NLog;
using System.Collections.Concurrent;
using System.Net;
using Newtonsoft.Json.Bson;
using Newtonsoft.Json.Linq;

namespace DtronixJsonRpc {

	/// <summary>
	/// Client to connect and communicate on the JSON RPC protocol.
	/// </summary>
	/// <typeparam name="THandler">Action Handler to contain all action class instances.</typeparam>
	public class JsonRpcClient<THandler> : IDisposable
		where THandler : ActionHandler<THandler>, new() {

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
		/// Event called when a client has changed status. (Connected, Disconnected).
		/// May not be called if the server is not configured to broadcast changes.
		/// </summary>
		public event EventHandler<JsonRpcClient<THandler>, ConnectedClientChangedEventArgs> OnConnectedClientChange;

		/// <summary>
		/// Event called by the server to verify the client's authentication challenge.
		/// </summary>
		internal event EventHandler<JsonRpcClient<THandler>, ConnectorAuthenticationEventArgs> OnAuthenticationVerify;

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


		internal Stopwatch ping_stopwatch;

		private const int AUTH_TIMEOUT = 2000;

		/// <summary>
		/// Creates a new Client to connect to  and communicate with a JSON RPC server.
		/// </summary>
		/// <param name="address">Address the server is located.</param>
		/// <param name="port">Port the server is bound to.</param>
		/// <returns>New configured instance of a JsonRpcClient.</returns>
		public static JsonRpcClient<THandler> CreateClient(string address, int port = 2828) {
			return new JsonRpcClient<THandler>(-1) {
				client = new TcpClient(),
				Mode = JsonRpcSource.Client,
				Port = port,
				Address = address
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
			return new JsonRpcClient<THandler>(id) {
				Server = server,
				client = client,
				Mode = JsonRpcSource.Server,
				Port = server.Configurations.BindingPort,
				Address = ((IPEndPoint)client.Client.RemoteEndPoint).Address.ToString(),
				ping_stopwatch = new Stopwatch()
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
		}

		/// <summary>
		/// Method to provide authentication verification from the server side.
		/// </summary>
		/// <returns>Returns on successful authentication. False otherwise.</returns>
		protected virtual bool AuthenticateClient() {
			try {
				string failure_reason = null;

				// Read the user info object.
				var user_info = Read().ToObject<JsonRpcParam<ClientInfo>>();

				// Send the ID to the client
				Send(new JsonRpcParam<int>(null, Info.Id), true);

				// Read the authentication "Data" text.
				JsonRpcParam<string> authentication_text = Read()?.ToObject<JsonRpcParam<string>>();

				// Check to ensure a valid user info class was passed.
				if (user_info == null) {
					failure_reason = "User information passed was invalid.";
				}

				// Clean the username.
				Info.Username = user_info?.Args.Username?.Trim();

				// Check to ensure the client should connect.
				if (Server.Clients.Values.Any(cli => cli.Info.Id != Info.Id && cli.Info.Username == Info.Username)) {
					failure_reason = "Duplicate username on server.";
				}

				// Check to see if a username was provided.
				if (string.IsNullOrEmpty(Info.Username)) {
					failure_reason = "Did not provide a username.";
				}

				// Authorize client.
				var auth_args = new ConnectorAuthenticationEventArgs() {
					Data = authentication_text.Args
				};

				// Verify the client against the event set.
				if (OnAuthenticationVerify != null) {

					// Fire the event to verify that the client has the proper data.
					OnAuthenticationVerify?.Invoke(this, auth_args);

					// Verify whether or not the client has passed the challenge.
					if (auth_args.Authenticated) {
						Info.Status = ClientStatus.Connected;
					} else {
						failure_reason = auth_args.FailureReason;
					}


				} else {

					// Client has successfully authenticated.
					Info.Status = ClientStatus.Connected;
				}

				
				if (Info.Status != ClientStatus.Connected || failure_reason != null) {

					// Inform the client that it failed authentication before disconnecting it.
					Send(new JsonRpcParam<string>("$" + nameof(OnAuthenticationFailure), failure_reason), true);

					// Disconnect the client if it has not passed authentication.
					Disconnect("Authentication Failed. Reason: " + failure_reason);
					return false;

				} else {
					// Inform the client that it has successfully authenticated.
					Send(new JsonRpcParam<string>("$OnAuthenticationSuccess"), true);

					// Invoke the client connect event on the server.
					OnConnect?.Invoke(this, new ClientConnectEventArgs<THandler>(Server, this));
				}


			} catch (OperationCanceledException) {

				// Called if the authentication timeout has occurred.
				Disconnect("Client authentication timeout.");
				return false;
			}

			// Inform all the other clients of the new connection if desired.
			if (Server.Configurations.BroadcastClientStatusChanges) {

				// Broadcast to all the other clients that there is a new connection if desired.
				Server.Broadcast(cl => {

					// This client will know it is connected by other means.
					if (cl?.Info.Id == Info.Id) {
						return;
					}

					// Send the info to the client.
					cl.Send(new JsonRpcParam<ClientInfo[]>("$" + nameof(OnConnectedClientChange), new ClientInfo[] { Info }));
				});
			}

			// Inform this client that it has connected and send it all the connected clients.
			if (Server.Configurations.BroadcastClientStatusChanges) {
				Send(new JsonRpcParam<ClientInfo[]>("$" + nameof(OnConnectedClientChange), Server.Clients.Select(cl => cl.Value.Info).ToArray()), true);
			}

			logger.Debug("{0} CID {1}: Successfully authorized on the server.", Mode, Info.Id);

			return true;
		}

		/// <summary>
		/// Requests authentication verification from the client side.
		/// </summary>
		protected virtual void RequestAuthentication() {
			try {
				// Send this client info to the server.
				Send(new JsonRpcParam<ClientInfo>(null, Info), true);

				// Read the ID from the server
				var uid_args = Read().ToObject<JsonRpcParam<int>>();
				Info.Id = uid_args.Args;

				// Authorize the client with the specified events.
				var auth_args = new ConnectorAuthenticationEventArgs();

				OnAuthenticationRequest?.Invoke(this, auth_args);

				Send(new JsonRpcParam<string>(null, auth_args.Data ?? ""), true);

			} catch (OperationCanceledException) {
				Disconnect("Server authentication timed out..", Mode);
			}
		}

		public void Connect() {

			try {

				logger.Info("{0} CID {1}: New client started", Mode, Info.Id);
				Info.Status = ClientStatus.Connecting;

				if (Mode == JsonRpcSource.Client) {
					logger.Info("{0} CID {1}: Attempting to connect to server", Mode, Info.Id);
					var completed = client.ConnectAsync(Address, Port).Wait(3000, cancellation_token_source.Token);

					if (completed == false && client.Connected == false) {
						logger.Warn("{0} CID {1}: Attempted connection did not complete successfully.", Mode, Info.Id);
						Disconnect("Could not connect client in a reasonable amount of time.", JsonRpcSource.Client, SocketError.TimedOut);
						return;
					}

				}

				base_stream = client.GetStream();

				if(Server.Configurations.DataMode == JsonRpcServerConfigurations.JsonMode.Bson) {
					writer = new BsonWriter(base_stream);
					reader = new BsonReader(base_stream);

				}else if (Server.Configurations.DataMode == JsonRpcServerConfigurations.JsonMode.Json) {
					// We have to use stream readers and writers because the JSON text writer/reader has to have a text reader/writer.
					stream_writer = new StreamWriter(base_stream, Encoding.UTF8, 1024 * 16, true);
					stream_reader = new StreamReader(base_stream, Encoding.UTF8, true, 1024 * 16, true);

					writer = new JsonTextWriter(stream_writer);
					reader = new JsonTextReader(stream_reader);
				}

				// Don't indent the code
				writer.Formatting = Formatting.None;
				reader.SupportMultipleContent = true;

				logger.Debug("{0} CID {1}: Connected. Authenticating...", Mode, Info.Id);

				// If the client has not authenticated withing the limit, kick it.
				Task.Delay(AUTH_TIMEOUT).ContinueWith(task => {
					if(Info.Status == ClientStatus.Connecting) {
						Disconnect("Client did not authenticate within time limitation.");
					}
                });

				if (Mode == JsonRpcSource.Server) {
					if (AuthenticateClient() == false) {
						logger.Debug("{0} CID {1}: Authentication failure.", Mode, Info.Id);
						return;
					}

					logger.Debug("{0} CID {1}: Authorized", Mode, Info.Id);
				} else {
					RequestAuthentication();
				}

				Info.Status = ClientStatus.Connected;

				while (cancellation_token_source.IsCancellationRequested == false) {
					JToken data;
					// See if we have reached the end of the stream.
					data = Read();
					if (data == null) {
						return;
					}
					string method = data["method"].Value<string>();

					logger.Debug("{0} CID {1}: Method '{2}' called", Mode, Info.Id, data["method"].Value<string>());

					try {
						if (method.StartsWith("$")) {
							ExecuteSpecialAction(method, data);
						} else {
							Actions.ExecuteAction(method, data);
						}
						
					} catch (Exception e) {
						logger.Error(e, "{0} CID {1}: Called action threw exception. Exception: {2}", Mode, Info.Id, e.ToString());
						throw e;
					}
				}
			} catch (SocketException e) {
				Disconnect("Server connection issues", JsonRpcSource.Client, e.SocketErrorCode);

			} catch (Exception e) {
				logger.Error(e, "{0} CID {1}: Exception Occurred: {2}", Mode, Info.Id, e.ToString());
				throw e;

			} finally {
				if (Info.Status != ClientStatus.Disconnected && Info.Status != ClientStatus.Disconnecting) {
					Disconnect("Client closed", Mode);
				}
			}
		}


		internal void ExecuteSpecialAction(string method, JToken data) {
			ClientInfo[] clients_info;

			if (method == "$" + nameof(OnConnectedClientChange)) {

				clients_info = data.ToObject<JsonRpcParam<ClientInfo[]>>().Args;
				OnConnectedClientChange?.Invoke(this, new ConnectedClientChangedEventArgs(clients_info));

			} else if (method == "$" + nameof(OnDisconnect)) {
				clients_info = data.ToObject<JsonRpcParam<ClientInfo[]>>().Args;
				Disconnect(clients_info[0].DisconnectReason, (Mode == JsonRpcSource.Client) ? JsonRpcSource.Server : JsonRpcSource.Client);

			} else if (method == "$" + nameof(OnAuthenticationFailure)) {
				if (Mode == JsonRpcSource.Client) {
					logger.Debug("{0} CID {1}: Authorized", Mode, Info.Id);
					var reason = data.ToObject<JsonRpcParam<string>>().Args;
					OnAuthenticationFailure?.Invoke(this, new AuthenticationFailureEventArgs(reason));
				}

			} else if (method == "$OnAuthenticationSuccess") {
				// If this is the client, enable the ping timer.
				if (Mode == JsonRpcSource.Client) {
					ping_timer.Enabled = true;
				}

				OnConnect?.Invoke(this, new ClientConnectEventArgs<THandler>(Server, this));

			} else if (method == "$ping") {
				var source = data.ToObject<JsonRpcParam<JsonRpcSource>>().Args;

				if (source == Mode) {
					ping_stopwatch.Stop();
					Ping = ping_stopwatch.ElapsedMilliseconds;
					Send(new JsonRpcParam<long>("$ping-result", Ping));

					logger.Trace("{0} CID {1}: Ping {2}ms", Mode, Info.Id, Ping);

				} else {
					// Ping back immediately.
					Send(new JsonRpcParam<JsonRpcSource>("$ping", source));
				}

			} else if (method == "$ping-result") {
				var ping = data.ToObject<JsonRpcParam<long>>().Args;
				Ping = ping;

			} else {
				Disconnect("Tired to call invalid method. Check client version.");
			}

		}

		
		private JToken Read() {
			JToken data;
			try {

				reader.Read();
				data = JToken.ReadFrom(reader);

			} catch (OperationCanceledException e) {
				logger.Debug(e, "{0} CID {1}: Method reader was canceled", Mode, Info.Id);
				return null;

			} catch (SocketException e) {
				logger.Warn(e, "{0} CID {1}: Socket Exception occurred while listening. Exception: {2}", Mode, Info.Id, e.InnerException.ToString());
				Disconnect("Connection closed", Mode);
				return null;

			} catch (IOException e) {

				logger.Warn(e, "{0} CID {1}: IO Exception occurred while listening. Exception: {2}", Mode, Info.Id, e.InnerException.ToString());
				Disconnect("Connection closed", Mode);
				return null;

			} catch (JsonReaderException e) {
				logger.Warn(e, "{0} CID {1}: JSON parsing Exception occurred. Exception: {2}", Mode, Info.Id, e.ToString());
				Disconnect("Connection closed", Mode);
				return null;

			} catch (Exception e) {
				logger.Error(e, "{0} CID {1}: Unknown exception occurred while listening. Exception: {2}", Mode, Info.Id, e.ToString());
				Disconnect("Connection closed", Mode);
				return null;
			}

			logger.Trace("{0} CID {1}: Read object from stream: {2}", Mode, Info.Id, data.ToString(Formatting.None));

			return data;
		}


		internal void Send<T>(JsonRpcParam<T> args, bool force_send = false) {
			if (cancellation_token_source.IsCancellationRequested) {
				return;
			}

			if (client.Client.Connected == false) {
				return;
			}

			if (Info.Status == ClientStatus.Connecting && force_send == false) {
				throw new InvalidOperationException("Can not send request while still connecting.");
			}

			// Performance Hit.
			logger.Trace("{0} CID {1}: Write line to stream: {2}", Mode, Info.Id, JsonConvert.SerializeObject(args));

			try {
				lock (write_lock) {
					serializer.Serialize(writer, args);
					writer.Flush();
				}
			} catch (IOException e) {
				if (client.Client.Connected) {
					logger.Error(e, "{0} CID {1}: Exception occurred when trying to write to the stream. Exception: {2}", Mode, Info.Id, e.ToString());
				}
				Disconnect("Writing error to stream.", Mode);

			} catch (ObjectDisposedException e) {
				logger.Warn("{0} CID {1}: Tried to write to the stream when the client was closed.", Mode, Info.Id);
				// The client was closed.  
				return;

			} catch (OperationCanceledException) {
				return;

			} catch (Exception e) {
				logger.Error(e, "{0} CID {1}: Unknown error occurred while writing to the stream. Exception: {2}", Mode, Info.Id, e.ToString());
				throw e;
			}
		}

		public void Disconnect(string reason) {
			Disconnect(reason, Mode, SocketError.Success);
		}

		public void Disconnect(string reason, JsonRpcSource source) {
			Disconnect(reason, source, SocketError.Success);
		}

		public void Disconnect(string reason, JsonRpcSource source, SocketError socket_error) {
			if (string.IsNullOrWhiteSpace(reason)) {
				throw new ArgumentException("Reason for closing connection can not be null or empty.");
			}

			logger.Info("{0} CID {1}: Stop requested. Reason: {2}", Mode, Info.Id, reason);

			if (Info.Status == ClientStatus.Disconnecting) {
				logger.Debug("{0} CID {1}: Stop requested but client is already in the process of stopping.", Mode, Info.Id);
				return;
			}
			Info.Status = ClientStatus.Disconnecting;
			Info.DisconnectReason = reason;

			// If we are disconnecting, let the other party know.
			if (Mode == source && client.Client.Connected) {
				Send(new JsonRpcParam<ClientInfo[]>("$" + nameof(OnDisconnect), new ClientInfo[] { Info }));
			}

			OnDisconnect?.Invoke(this, new ClientDisconnectEventArgs<THandler>(reason, source, Server, this, socket_error));
			cancellation_token_source?.Cancel();
			client?.Close();

			// Close and dispose all streams.
			base_stream?.Dispose();
			stream_writer?.Dispose();
			stream_reader?.Dispose();
			writer?.Close();
			reader?.Close();

			// Stop the timer for the ping.
			ping_timer?.Dispose();
			ping_stopwatch?.Stop();

			Info.Status = ClientStatus.Disconnected;
			logger.Info("{0} CID {1}: Stopped", Mode, Info.Id);
		}


		public void Dispose() {
			Disconnect("Class object disposed.", Mode);
		}
	}
}

