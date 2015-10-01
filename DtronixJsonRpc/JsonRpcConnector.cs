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

namespace DtronixJsonRpc {
	public class JsonRpcConnector<THandler>
		where THandler : ActionHandler<THandler>, new() {

		private static Logger logger = LogManager.GetCurrentClassLogger();

		private readonly CancellationTokenSource cancellation_token_source = new CancellationTokenSource();

		public ClientInfo Info { get; private set; } = new ClientInfo();

		public string Address { get; private set; }
		public int Port { get; private set; } = 2828;

		private TcpClient client;
		private Stream base_stream;
		private StreamWriter client_writer;
		private StreamReader client_reader;
		private bool _IsStopping = false;

		public bool IsStopping {
			get {
				return _IsStopping;
			}
		}

		public THandler Actions { get; }

		private object lock_object = new object();

		public event EventHandler<JsonRpcConnector<THandler>, ClientDisconnectEventArgs> OnDisconnect;
		public event EventHandler<JsonRpcConnector<THandler>> OnConnect;
		public event EventHandler<JsonRpcConnector<THandler>, ConnectorAuthroizationEventArgs> OnAuthorizationRequest;
		public event EventHandler<JsonRpcConnector<THandler>, ConnectorAuthroizationEventArgs> OnAuthorizationVerify;
		public event EventHandler<JsonRpcConnector<THandler>, ActionMethodCallEventArgs> OnActionMethodCall;
		public event EventHandler<JsonRpcConnector<THandler>, ConnectedClientChangedEventArgs> OnConnectedClientChange;

		public JsonRpcSource Mode { get; protected set; }

		public JsonRpcServer<THandler> Server { get; private set; }

		public JsonRpcConnector(string address) {
			Actions = new THandler();
			Actions.Connector = this;
			Address = address;
			client = new TcpClient();
			Mode = JsonRpcSource.Client;
		}

		public JsonRpcConnector(JsonRpcServer<THandler> server, TcpClient client, int id) {
			Actions = new THandler();
			Actions.Connector = this;
			this.client = client;
			Info.Id = id;
			Server = server;
			Mode = JsonRpcSource.Server;
		}

		protected virtual void AuthorizeClient() {
			// Read the initial user info.
			var user_info_text = client_reader.ReadLine();
			var user_info = JsonConvert.DeserializeObject<ClientInfo>(user_info_text);

			if (user_info == null) {
				throw new InvalidDataException("User information passed was invalid.");
			}

			Info.Username = user_info.Username.Trim();

			// Send the ID to the client
			client_writer.WriteLine(Info.Id);
			client_writer.Flush();

			// Checks to ensure the client should connect.
			if (Server.Clients.Values.FirstOrDefault(cli => cli.Info.Id != Info.Id && cli.Info.Username == Info.Username) != null) {
				Disconnect("Duplicate username on server.", JsonRpcSource.Server);
				return;
			}

			Info.Status = ClientStatus.Connected;

			// Alert all the other clients of the new connection.
			Server.Broadcast(cl => {
				// Skip this client
				if (cl?.Info.Id == Info.Id) {
					return;
				}
				cl.Send("$" + nameof(OnConnectedClientChange), new ClientInfo[] { Info });
			});

			Send("$" + nameof(OnConnectedClientChange), Server.Clients.Select(cl => cl.Value.Info).ToArray());
			LogDebug("Successfully authorized on the server.");
		}

		protected virtual void RequestAuthorization() {
			client_writer.WriteLine(JsonConvert.SerializeObject(Info));
			client_writer.Flush();

			// Read the ID from the server
			var id = client_reader.ReadLine();

			int int_id;

			if (int.TryParse(id, out int_id) == false) {
				Disconnect("Server did not send a valid user id.", JsonRpcSource.Server);
			}

			Info.Id = int_id;
		}

		public void Connect() {

#pragma warning disable 4014
			Task.Factory.StartNew((main_task) => {
				//try {
				LogInfo("New client started");
				Info.Status = ClientStatus.Connecting;

				if (Mode == JsonRpcSource.Client) {
					LogInfo("Connecting...");
					var completed = client.ConnectAsync(Address, Port).Wait(3000, cancellation_token_source.Token);

					if (completed == false && client.Connected == false) {
						LogWarn("Attempted connection did not complete successfully.");
						Disconnect("Could not connect client in a reasonable amount of time.", JsonRpcSource.Client, SocketError.TimedOut);
						return;
					}

				}

				base_stream = client.GetStream();
				client_writer = new StreamWriter(base_stream, Encoding.UTF8, 1024 * 16, true);
				client_reader = new StreamReader(base_stream, Encoding.UTF8, true, 1024 * 16, true);

				LogDebug("Connected. Authorizing...");
				if (Mode == JsonRpcSource.Server) {
					AuthorizeClient();
				} else {
					RequestAuthorization();
				}

				LogDebug("Authorized");

				Info.Status = ClientStatus.Connected;

				OnConnect?.Invoke(this, this);
				while (cancellation_token_source.IsCancellationRequested == false) {

					// Get the type.
					Task<string> type_task = null;
					try {
						type_task = client_reader.ReadLineAsync();//.WithCancellation(cancellation_token_source.Token);
						type_task.Wait(cancellation_token_source.Token);

						// See if we have reached the end of the stream.
						if (type_task.Result == null) {
							Disconnect("Connection closed", Mode);
							return;
						}
					} catch (TaskCanceledException) {
						return;
					} catch (Exception) {
						Disconnect("Connection closed", Mode);
						return;
					}

					// PArse the type.
					Type cli_type;
					try {
						cli_type = Type.GetType(type_task.Result);
					} catch (Exception) {
						Disconnect("Sent invalid type.", Mode);
						return;
					}

					var type_element = cli_type.GetElementType();

					// If the class is not a subclass of one of the models, then something suspicious is going on.
					var sent_type_subclass = cli_type.IsSubclassOf(typeof(JsonRpcActionArgs));
					var sent_element_subclass = type_element?.IsSubclassOf(typeof(JsonRpcActionArgs));
					if (sent_type_subclass == false && sent_element_subclass == false) {
						Disconnect("Sent invalid type", Mode);
						return;
					}

					// Get the method called.
					var method = client_reader.ReadLine();

					if (method == null) {
						Disconnect("Send invalid method", Mode);
						return;
					}

					LogDebug($"Method '{method}' called");

					var json_data = JsonConvert.DeserializeObject(client_reader.ReadLine(), cli_type);

					try {
						if (method[0] == '$') {
							ExecuteSpecialAction(method, json_data);
						} else {
							Actions.ExecuteAction(method, json_data);
						}
					} catch (Exception e) {
						LogError(e, "Connector-{0} {1}: Action threw exception", Mode, Info.Id);
					}
				}

			}, TaskCreationOptions.LongRunning, cancellation_token_source.Token).ContinueWith(task => {
				if (cancellation_token_source.IsCancellationRequested) {
					Disconnect("Client closed", Mode);
					return;
				}

				try {
					var base_exception = task.Exception.GetBaseException();

					if (base_exception is SocketException) {
						var socket_exception = base_exception as SocketException;
						Disconnect("Server connection issues", JsonRpcSource.Client, socket_exception.SocketErrorCode);
					} else {
						LogError(base_exception, "Exception Occurred", Mode, Info.Id);
					}
				} catch (Exception ex) {
					LogError(ex, "Connector-{0} {1}: Exception Occurred", Mode, Info.Id);
					throw;
				}

			}, TaskContinuationOptions.AttachedToParent);
		}

		private void ExecuteSpecialAction(string method, object json_data) {
			switch (method) {
				case "$" + nameof(OnConnectedClientChange):
					OnConnectedClientChange?.Invoke(this, new ConnectedClientChangedEventArgs(json_data as ClientInfo[]));
					break;

				case "$" + nameof(OnDisconnect):
					var ci = json_data as ClientInfo[];
					Disconnect(ci[0].DisconnectReason, JsonRpcSource.Server);
					break;
			}
		}

		public void Disconnect(string reason, JsonRpcSource source, SocketError socket_error = SocketError.Success) {
			LogInfo("Connector-{0} {1}: Stop requested. Reason: {2}", Mode, Info.Id, reason);

			if (Info.Status == ClientStatus.Disconnecting) {
				LogDebug("Connector-{0} {1}: Stop requested but client is already in the process of stopping.");
				return;
			}
			Info.Status = ClientStatus.Disconnecting;

			// If this is the server, let the client know they are being disconnected.
			if (Mode == JsonRpcSource.Server) {
				Send("$OnDisconnect", new ClientInfo[] { Info });
			}

			OnDisconnect?.Invoke(this, new ClientDisconnectEventArgs(reason, source, socket_error));
			cancellation_token_source.Cancel();
			client.Close();

			Info.Status = ClientStatus.Disconnected;
		}


		public void Send(string method, object json = null) {
			if (Info.Status == ClientStatus.Disconnecting || Info.Status == ClientStatus.Disconnected) {
				return;
			}

			LogDebug("Sending method '{0}'", new object[] { method });

			lock (lock_object) {
				try {
					client_writer.WriteLine(json.GetType().AssemblyQualifiedName);
					client_writer.WriteLine(method);
					if (json == null) {
						client_writer.WriteLine();
					} else {
						client_writer.WriteLine(JsonConvert.SerializeObject(json));
					}
					client_writer.Flush();
				} catch (ObjectDisposedException) {
					// The client was closed.  
					return;
				}

			}

		}


		private void LogDebug(string log,
			object[] args = null,
			[System.Runtime.CompilerServices.CallerLineNumber] int line_number = 0,
			[System.Runtime.CompilerServices.CallerMemberName] string member_name = "",
			[System.Runtime.CompilerServices.CallerFilePath] string sourceFilePath = "") {

			logger.Debug("Connector-{0} {1} [{2}:{3}():{4}]: " + string.Format(log, args), Mode, Info.Id, Path.GetFileName(sourceFilePath), member_name, line_number);
		}

		private void LogInfo(string log, params object[] args) {
			logger.Info("Connector-{0} {1}: " + string.Format(log, args), Mode, Info.Id, log);
		}

		private void LogError(Exception e, string log, params object[] args) {
			logger.Error(e, "Connector-{0} {1}: " + string.Format(log, args), Mode, Info.Id, log);
		}

		private void LogWarn(string log, params object[] args) {
			logger.Warn("Connector-{0} {1}: " + string.Format(log, args), Mode, Info.Id, log);
		}

	}
}
