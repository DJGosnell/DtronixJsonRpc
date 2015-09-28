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

		private int _Id;

		public int Id {
			get { return _Id; }
			set { _Id = value; }
		}


		private TcpClient client;

		public ClientInfo Info { get; set; } = new ClientInfo();

		public string Address { get; private set; }

		private int port = 2828;
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

		private string token;

		public static JsonRpcConnector CreateClient(string address) {
			return new JsonRpcConnector() {
				Address = address,
				password = password,
				_LocalActions = actions,
				mode = JsonRpcMode.Client,
				client = new TcpClient()
			};
		}

		public static JsonRpcConnector<ServerActions, ClientActions> CreateServer(Core core, TcpClient client, string password, ServerActions actions, int id) {
			return new JsonRpcConnector<ServerActions, ClientActions>() {
				core = core,
				client = client,
				_LocalActions = actions,
				password = password,
				mode = JsonRpcMode.Server,
				Id = id
			};
		}

		public void Start() {

#pragma warning disable 4014
			Task.Factory.StartNew((main_task) => {
				//try {
				LogLine($"New client started in {mode.ToString()} mode with the ID {_Id}.");
				if (client.Connected == false) {
					LogLine("Connecting...");
					var completed = client.ConnectAsync(Address, port).Wait(3000, cancellation_token_source.Token);

					if (completed == false && client.Connected == false) {
						LogLine("Attempted connection did not complete successfully.");
						Stop("Could not connect client in a reasonable amount of time.", mode, SocketError.TimedOut);
						return;
					}

				}

				base_stream = client.GetStream();
				client_writer = new StreamWriter(base_stream, Encoding.UTF8, 1024 * 16, true);
				client_reader = new StreamReader(base_stream, Encoding.UTF8, true, 1024 * 16, true);

				LogLine("Connected.");
				if (mode == JsonRpcMode.Server) {
					// Read the initial user info.
					var user_info_text = client_reader.ReadLine();
					var user_info = JsonConvert.DeserializeObject(user_info_text, typeof(ClientInfoObject)) as ClientInfoObject;

					if (user_info == null) {
						Stop("User information passed was invalid.", mode);
						return;
					}



					Info = user_info;
					Info.Id = Id;
					Info.Username = Info.Username.Trim();

					// Send the ID to the client
					client_writer.WriteLine(Id);
					client_writer.Flush();

					// Checks to ensure the client should connect.
					if (core.Server.Clients.Values.FirstOrDefault(cli => cli._Id != _Id && cli.Info.Username == Info.Username) != null) {
						Stop("Duplicate username on server.", mode);
						return;
					}



					// Alert all the other clients of the new connection.
					core.Server.Broadcast(cl => {
						// Skip this client
						if (cl.Id == this.Id) {
							return;
						}
						cl.Send(nameof(ClientActions.UpdateClient), new ClientInfoObject[] { user_info });
					});



					Send(nameof(ClientActions.UpdateClient), core.Server.Clients.Select(cl => cl.Value.Info).ToArray());
					LogLine("Client successfully authorized on the server.");
				} else if (mode == JsonRpcMode.Client) {
					LogLine("Client connected.");
					client_writer.WriteLine(JsonConvert.SerializeObject(Info));
					client_writer.Flush();

					// Read the ID from the server
					var id = client_reader.ReadLine();

					if (int.TryParse(id, out _Id) == false) {
						Stop("Server did not send a valid user id.", mode);
					}
				}

				Connected = true;

				OnConnect?.Invoke(this, this);
				while (cancellation_token_source.IsCancellationRequested == false) {
					Task<string> type_task = null;
					try {
						type_task = client_reader.ReadLineAsync().WithCancellation(cancellation_token_source.Token);
						type_task.Wait();
					} catch (TaskCanceledException) {

					} catch (Exception) {
						Stop("Connection closed", mode);
						return;
					}

					var method = client_reader.ReadLine();

					if (method == null) {
						Stop("Send invalid method", mode);
						return;
					}

					Type cli_type;
					try {
						cli_type = Type.GetType(type_task.Result);
					} catch (Exception) {
						Stop("Sent invalid type.", mode);
						return;
					}


					var type_element = cli_type.GetElementType();

					// If the class is not a subclass of one of the models, then something suspicious is going on.
					if (type_element == null || type_element.IsSubclassOf(typeof(ActionArgs)) == false) {
						Stop("Sent invalid type", mode);
						return;
					}
					LogLine($"Client called method '{method}'", $"Server called method '{method}'");

					var json_data = JsonConvert.DeserializeObject(client_reader.ReadLine(), cli_type);

					_LocalActions.ExecuteAction(method, json_data, this);
				}
				/*} catch (TaskCanceledException) {
                    Stop("Client closed", mode);
                    return;
                } catch (Exception e) {
                    LogLine("Exception Occurred: " + e.ToString());
                    throw;
                }*/

			}, TaskCreationOptions.LongRunning, cancellation_token_source.Token).ContinueWith(task => {
				if (cancellation_token_source.IsCancellationRequested) {
					Stop("Client closed", mode);
					return;
				}

				try {
					var base_exception = task.Exception.GetBaseException();

					if (base_exception is SocketException) {
						var socket_exception = base_exception as SocketException;
						Stop("Server connection issues", JsonRpcMode.Client, socket_exception.SocketErrorCode);
					} else {
						LogLine("Exception Occurred: " + base_exception.ToString());
					}
				} catch (Exception ex) {
					LogLine("Exception Occurred: " + ex.ToString());
					throw;
				}

			}, TaskContinuationOptions.AttachedToParent);
		}

		public void Stop(string reason, JsonRpcMode source, SocketError socket_error = SocketError.Success) {
			LogLine("Stop requested. Reason: " + reason);
			if (_IsStopping) {
				LogLine("Stop requested but client is already in the process of stopping.");
				return;
			}
			_IsStopping = true;

			// If this is the server, let the client know they are being disconnected.
			(RemoteActions as ClientActions)?.Disconnect(new ClientActions.DisconnectArgs() { Reason = reason, StopSource = JsonRpcMode.Server });


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
			if (mode == JsonRpcMode.Server) {
				Debug.WriteLine($"Server (Client {Info.Id}): " + server);
			} else {
				Debug.WriteLine($"Client {Info.Id}: " + client);
			}
		}

	}
}
