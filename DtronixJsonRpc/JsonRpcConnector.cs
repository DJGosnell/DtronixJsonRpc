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

namespace DtronixJsonRpc {
	public class JsonRpcConnector {

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

		private object lock_object = new object();

		public event EventHandler<JsonRpcConnector, ClientDisconnectEventArgs> OnDisconnect;
		public event EventHandler<JsonRpcConnector> OnConnect;
		public event EventHandler<JsonRpcConnector, ConnectorAuthroizationEventArgs> OnAuthorizationRequest;
		public event EventHandler<JsonRpcConnector, ConnectorAuthroizationEventArgs> OnAuthorizationVerify;
		public event EventHandler<JsonRpcConnector, ActionMethodCallEventArgs> OnActionMethodCall;
		public event EventHandler<JsonRpcConnector, ConnectedClientChangedEventArgs> OnConnectedClientChange;

		public JsonRpcSource Mode { get; protected set; }

		public JsonRpcServer Server { get; private set; }

		public JsonRpcConnector(string address) {
			Address = address;
			client = new TcpClient();
			Mode = JsonRpcSource.Client;

		}

		public JsonRpcConnector(JsonRpcServer server, TcpClient client, int id) {
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
			if (Server.Clients.Values.FirstOrDefault(cli => cli._Id != Info.Id && cli.Info.Username == Info.Username) != null) {
				Disconnect("Duplicate username on server.", JsonRpcSource.Server);
				return;
			}

			// Alert all the other clients of the new connection.
			Server.Broadcast(cl => {
				// Skip this client
				if (cl?.Info.Id == Info.Id) {
					return;
				}
				cl.Send("$OnConnectedClientChange", new ClientInfo[] { Info });
			});

			Send("$OnConnectedClientChange", Server.Clients.Select(cl => cl.Value.Info).ToArray());
			LogLine("Client successfully authorized on the server.");
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
				LogLine($"New client started with the ID {Info.Id}.");
				Info.Status = ClientStatus.Connecting;

				if (Mode == JsonRpcSource.Client) {
					LogLine("Connecting...");
					var completed = client.ConnectAsync(Address, Port).Wait(3000, cancellation_token_source.Token);

					if (completed == false && client.Connected == false) {
						LogLine("Attempted connection did not complete successfully.");
						Disconnect("Could not connect client in a reasonable amount of time.", JsonRpcSource.Client, SocketError.TimedOut);
						return;
					}

				}

				base_stream = client.GetStream();
				client_writer = new StreamWriter(base_stream, Encoding.UTF8, 1024 * 16, true);
				client_reader = new StreamReader(base_stream, Encoding.UTF8, true, 1024 * 16, true);

				LogLine("Connected.");
				if (Mode == JsonRpcSource.Server) {
					AuthorizeClient();
				} else {
					RequestAuthorization();
                }

				Info.Status = ClientStatus.Connected;

				OnConnect?.Invoke(this, this);
				while (cancellation_token_source.IsCancellationRequested == false) {

					// Get the type.
					Task<string> type_task = null;
					try {
						type_task = client_reader.ReadLineAsync();//.WithCancellation(cancellation_token_source.Token);
						type_task.Wait(cancellation_token_source.Token);
					} catch (TaskCanceledException) {
						return;
					} catch (Exception) {
						Disconnect("Connection closed", (Server == null) ? JsonRpcSource.Client | JsonRpcSource.Server);
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
					if (type_element == null || type_element.IsSubclassOf(typeof(JsonRpcActionArgs)) == false) {
						Disconnect("Sent invalid type", Mode);
						return;
					}

					// Get the method called.
					var method = client_reader.ReadLine();

					if (method == null) {
						Disconnect("Send invalid method", Mode);
						return;
					}

					LogLine($"Client called method '{method}'", $"Server called method '{method}'");

					var json_data = JsonConvert.DeserializeObject(client_reader.ReadLine(), cli_type);

					try {

					} catch {
					}
					if (method[0] == '$') {
						ExecuteSpecialMethod(method, json_data);
					} else {
						_LocalActions.ExecuteAction(method, json_data, this);
					}



				}
				/*} catch (TaskCanceledException) {
                    Disconnect("Client closed", mode);
                    return;
                } catch (Exception e) {
                    LogLine("Exception Occurred: " + e.ToString());
                    throw;
                }*/

			}, TaskCreationOptions.LongRunning, cancellation_token_source.Token).ContinueWith(task => {
				if (cancellation_token_source.IsCancellationRequested) {
					Disconnect("Client closed", mode);
					return;
				}

				try {
					var base_exception = task.Exception.GetBaseException();

					if (base_exception is SocketException) {
						var socket_exception = base_exception as SocketException;
						Disconnect("Server connection issues", JsonRpcSource.Client, socket_exception.SocketErrorCode);
					} else {
						LogLine("Exception Occurred: " + base_exception.ToString());
					}
				} catch (Exception ex) {
					LogLine("Exception Occurred: " + ex.ToString());
					throw;
				}

			}, TaskContinuationOptions.AttachedToParent);
		}

		private void ExecuteSpecialMethod(string method, object json_data) {
			switch (method) {
				case "$OnConnectedClientChange":
					OnConnectedClientChange?.Invoke(this, new ConnectedClientChangedEventArgs(json_data as ClientInfo[]));
                    break;
			}
		}

		public void Disconnect(string reason, JsonRpcSource source, SocketError socket_error = SocketError.Success) {
			LogLine("Stop requested. Reason: " + reason);
			if (_IsStopping) {
				LogLine("Stop requested but client is already in the process of stopping.");
				return;
			}
			_IsStopping = true;

			// If this is the server, let the client know they are being disconnected.
			(RemoteActions as ClientActions)?.Disconnect(new ClientActions.DisconnectArgs() { Reason = reason, StopSource = JsonRpcSource.Server });


			OnDisconnect?.Invoke(this, new TheatreClientDisconnectArgs(reason, source, socket_error));
			cancellation_token_source.Cancel();
			client.Close();
		}


		public void Send(string method, object json = null) {
			if (client.Connected == false) {
				return;
			}

			LogLine($"Sending method '{method}'");

			lock (lock_object) {
				client_writer.WriteLine(json.GetType().ToString());
				client_writer.WriteLine(method);
				if (json == null) {
					client_writer.WriteLine();
				} else {
					client_writer.WriteLine(JsonConvert.SerializeObject(json));
				}
				client_writer.Flush();
			}

		}

		private void LogLine(string log,
			[System.Runtime.CompilerServices.CallerLineNumber] int line_number = 0,
			[System.Runtime.CompilerServices.CallerMemberName] string member_name = "",
			[System.Runtime.CompilerServices.CallerFilePath] string sourceFilePath = "") {
			LogLine(log, log, line_number, member_name, sourceFilePath);
		}

		private void LogLine(string server, string client,
			[System.Runtime.CompilerServices.CallerLineNumber] int line_number = 0,
			[System.Runtime.CompilerServices.CallerMemberName] string member_name = "",
			[System.Runtime.CompilerServices.CallerFilePath] string sourceFilePath = "") {
			Debug.Write($"[{Path.GetFileName(sourceFilePath)}:{member_name}():{line_number}]");
			if (Mode == JsonRpcSource.Server) {
				Debug.WriteLine($"Server (Client {Info.Id}): " + server);
			} else {
				Debug.WriteLine($"Client {Info.Id}: " + client);
			}
		}

	}
}
