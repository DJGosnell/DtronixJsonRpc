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
	public class JsonRpcConnector<THandler> : IDisposable
		where THandler : ActionHandler<THandler>, new() {

		private static Logger logger = LogManager.GetCurrentClassLogger();

		private readonly CancellationTokenSource cancellation_token_source = new CancellationTokenSource();

		public ClientInfo Info { get; private set; } = new ClientInfo();

		public string Address { get; private set; }
		public int Port { get; private set; } = 2828;

		private TcpClient client;
		private NetworkStream base_stream;
		//private StreamReader client_reader;
		private BsonReader reader;
		private BsonWriter writer;
		private JsonSerializer serializer;
		private object write_lock = new object();

		private BlockingCollection<byte[]> write_queue = new BlockingCollection<byte[]>();

		public THandler Actions { get; }

		/// <summary>
		/// Object that is referenced by the action handlers.
		/// </summary>
		public object DataObject { get; set; }

		public event EventHandler<JsonRpcConnector<THandler>, ClientDisconnectEventArgs<THandler>> OnDisconnect;
		public event EventHandler<JsonRpcConnector<THandler>, ClientConnectEventArgs<THandler>> OnConnect;
		public event EventHandler<JsonRpcConnector<THandler>, ConnectorAuthenticationEventArgs> OnAuthenticationRequest;
		public event EventHandler<JsonRpcConnector<THandler>, AuthenticationFailureEventArgs> OnAuthenticationFailure;
		public event EventHandler<JsonRpcConnector<THandler>, ConnectorAuthenticationEventArgs> OnAuthenticationVerify;
		public event EventHandler<JsonRpcConnector<THandler>, ConnectedClientChangedEventArgs> OnConnectedClientChange;

		public JsonRpcSource Mode { get; protected set; }

		public JsonRpcServer<THandler> Server { get; private set; }

		/// <summary>
		/// The number of milliseconds elapsed to execute a command from the client to the server.
		/// </summary>
		public long Ping { get; private set; } = -1;

		private Stopwatch ping_stopwatch;

		private System.Timers.Timer ping_timer;

		private const int AUTH_TIMEOUT = 2000;

		public JsonRpcConnector(string address, int port = 2828) {
			Actions = new THandler();
			Actions.Connector = this;
			Address = address;
			client = new TcpClient();
			Mode = JsonRpcSource.Client;
			Port = port;

			ping_stopwatch = new Stopwatch();
			ping_timer = new System.Timers.Timer(5000);

			ping_timer.Elapsed += (sender, e) => {
				ping_stopwatch.Restart();
				Send(new JsonRpcParam<JsonRpcSource>("$ping", Mode), true);
			};

			serializer = new JsonSerializer();
		}

		public JsonRpcConnector(JsonRpcServer<THandler> server, TcpClient client, int id) {
			Actions = new THandler();
			Actions.Connector = this;
			Address = ((IPEndPoint)client.Client.RemoteEndPoint).Address.ToString();
			this.client = client;
			Info.Id = id;
			Server = server;
			Mode = JsonRpcSource.Server;
			serializer = new JsonSerializer();
		}


		protected virtual bool AuthenticateClient() {
			// Read the initial user info.
			try {
				string failure_reason = null;

				// Read the user info object.
				var user_info = Read(AUTH_TIMEOUT).ToObject<JsonRpcParam<ClientInfo>>();

				// Send the ID to the client
				Send(new JsonRpcParam<int>("$", Info.Id), true);

				// Read the auth text.
				JsonRpcParam<string> authentication_text = Read(AUTH_TIMEOUT).ToObject<JsonRpcParam<string>>();


				if (user_info == null) {
					failure_reason = "User information passed was invalid.";
				}

				Info.Username = user_info.Args.Username.Trim();
				// Checks to ensure the client should connect.
				if (Server.Clients.Values.Any(cli => cli.Info.Id != Info.Id && cli.Info.Username == Info.Username)) {
					failure_reason = "Duplicate username on server.";
				}

				// Authorize client.
				var auth_args = new ConnectorAuthenticationEventArgs() {
					Data = authentication_text.Args
				};

				// Verify the client against the event set.
				if (OnAuthenticationVerify != null) {
					OnAuthenticationVerify?.Invoke(this, auth_args);

					if (auth_args.Authenticated) {
						Info.Status = ClientStatus.Connected;
					} else {
						failure_reason = auth_args.FailureReason;
					}


				} else {
					Info.Status = ClientStatus.Connected;
				}

				if (Info.Status != ClientStatus.Connected || failure_reason != null) {
					Send(new JsonRpcParam<string>("$" + nameof(OnAuthenticationFailure), failure_reason), true);
					Disconnect("Authentication Failed. Reason: " + failure_reason);
					return false;

				} else {
					Send(new JsonRpcParam<string>("$OnAuthenticationSuccess"), true);
					OnConnect?.Invoke(this, new ClientConnectEventArgs<THandler>(Server, this));
				}


			} catch (OperationCanceledException) {
				Disconnect("Client did not provide connection information in a timely manor.", Mode);
				return false;
			}

			// Alert all the other clients of the new connection.
			Server.Broadcast(cl => {
				// Skip this client
				if (cl?.Info.Id == Info.Id) {
					return;
				}
				cl.Send(new JsonRpcParam<ClientInfo[]>("$" + nameof(OnConnectedClientChange), new ClientInfo[] { Info }));
			});

			Send(new JsonRpcParam<ClientInfo[]>("$" + nameof(OnConnectedClientChange), Server.Clients.Select(cl => cl.Value.Info).ToArray()), true);
			logger.Debug("{0} CID {1}: Successfully authorized on the server.", Mode, Info.Id);

			return true;
		}

		protected virtual void RequestAuthentication() {
			try {
				Send(new JsonRpcParam<ClientInfo>("$", Info), true);

				// Read the ID from the server
				var uid_args = Read(AUTH_TIMEOUT).ToObject<JsonRpcParam<int>>();
				Info.Id = uid_args.Args;

				// Authorize the client with the specified events.
				var auth_args = new ConnectorAuthenticationEventArgs();

				OnAuthenticationRequest?.Invoke(this, auth_args);

				Send(new JsonRpcParam<string>("$", auth_args.Data ?? ""), true);

			} catch (OperationCanceledException) {
				Disconnect("Server did not provide connection information in a timely manor.", Mode);
			}
		}

		public void Connect() {


			try {
				// Start the writer.
				//WriteLoop();

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

				writer = new BsonWriter(base_stream);
				//client_writer = new StreamWriter(base_stream, Encoding.UTF8, 1024 * 16, true);
				reader = new BsonReader(Stream.Synchronized(base_stream));
				reader.SupportMultipleContent = true;

				logger.Debug("{0} CID {1}: Connected. Authenticating...", Mode, Info.Id);
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
					}
				}
			} catch (SocketException e) {
				Disconnect("Server connection issues", JsonRpcSource.Client, e.SocketErrorCode);

			} catch (Exception e) {
				logger.Error(e, "{0} CID {1}: Exception Occurred: {2}", Mode, Info.Id, e.ToString());

			} finally {
				if (Info.Status != ClientStatus.Disconnected || Info.Status == ClientStatus.Disconnecting) {
					Disconnect("Client closed", Mode);
				}
			}
		}


		internal async void ExecuteSpecialAction(string method, JToken data) {
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
			base_stream?.Dispose();
			ping_timer?.Dispose();
			ping_stopwatch?.Stop();

			Info.Status = ClientStatus.Disconnected;
			logger.Info("{0} CID {1}: Stopped", Mode, Info.Id);
		}

		/*private void WriteLoop() {
			Task.Factory.StartNew(() => {
				try {
					foreach (byte[] buffer in write_queue.GetConsumingEnumerable(cancellation_token_source.Token)) {
						if (client.Client.Connected) {
							base_stream.Write(buffer, 0, buffer.Length);
						}
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
					throw;
				}

			}, TaskCreationOptions.LongRunning);
		}*/

		internal JToken Read(int timeout = -1) {
			var cancel = new CancellationTokenSource(timeout);
			JToken data;
			try {
				var task = Task.Run(() => {
					reader.Read();
					return JToken.ReadFrom(reader);
				}, cancel.Token);

				task.Wait();
				data = task.Result;
			} catch (OperationCanceledException e) {
				logger.Debug(e, "{0} CID {1}: Method reader was canceled", Mode, Info.Id);
				return null;
			} catch (IOException e) {
				logger.Error(e, "{0} CID {1}: IO Exception occurred while listening. Exception: {2}", Mode, Info.Id, e.InnerException.ToString());
				Disconnect("Connection closed", Mode);
				return null;

			} catch (AggregateException e) {
				Exception base_exception = e.GetBaseException();

				if (base_exception is IOException) {
					if (client.Client.Connected) {
						logger.Error(e, "{0} CID {1}: IO Exception occurred while listening. Exception: {2}", Mode, Info.Id, base_exception.ToString());
					} else {
						logger.Warn(e, "{0} CID {1}: Connection closed by the other party. Exception: {2}", Mode, Info.Id, base_exception.ToString());
					}
				} else if (base_exception is ObjectDisposedException) {
					logger.Warn(e, "{0} CID {1}: Connection closed by the other party. Exception: {2}", Mode, Info.Id, base_exception.ToString());
				} else {
					logger.Error(e, "{0} CID {1}: Unknown exception occurred while listening. Exception: {2}", Mode, Info.Id, base_exception.ToString());
				}

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

			lock (write_lock) {
				serializer.Serialize(writer, args);
			}
			
		}

		public void Dispose() {
			Disconnect("Class object disposed.", Mode);
		}
	}
}

