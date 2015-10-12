﻿using System;
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
		private StreamReader client_reader;

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
		/// The number of miliseconds elapsed to execute a command from the client to the server.
		/// </summary>
		public long Ping { get; private set; } = -1;

		private Stopwatch ping_stopwatch;

		private System.Timers.Timer ping_timer;

		private const int AUTH_TIMEOUT = 2000;

		public JsonRpcConnector(string address) {
			Actions = new THandler();
			Actions.Connector = this;
			Address = address;
			client = new TcpClient();
			Mode = JsonRpcSource.Client;

			ping_stopwatch = new Stopwatch();
			ping_timer = new System.Timers.Timer(5000);
			ping_timer.Elapsed += Ping_timer_Elapsed;
		}

		private void Ping_timer_Elapsed(object sender, System.Timers.ElapsedEventArgs e) {
			ping_stopwatch.Restart();
			Send("$ping", Mode);
		}

		public JsonRpcConnector(JsonRpcServer<THandler> server, TcpClient client, int id) {
			Actions = new THandler();
			Actions.Connector = this;
			this.client = client;
			Info.Id = id;
			Server = server;
			Mode = JsonRpcSource.Server;
		}

		protected virtual bool AuthenticateClient() {
			// Read the initial user info.
			try {
				string failure_reason = null;

				// Read the user info object.
				string user_info_text;
				TryReadLine(out user_info_text, AUTH_TIMEOUT);

				// Send the ID to the client
				WriteLine(Info.Id.ToString());

				// Read the auth text.
				string authentication_text;
				TryReadLine(out authentication_text, AUTH_TIMEOUT);

				// Parse the user info into an object.
				var user_info = JsonConvert.DeserializeObject<ClientInfo>(user_info_text);

				if (user_info == null) {
					failure_reason = "User information passed was invalid.";
				}

				Info.Username = user_info.Username.Trim();
				// Checks to ensure the client should connect.
				if (Server.Clients.Values.FirstOrDefault(cli => cli.Info.Id != Info.Id && cli.Info.Username == Info.Username) != null) {
					failure_reason = "Duplicate username on server.";
				}

				// Authorize client.
				var auth_args = new ConnectorAuthenticationEventArgs() {
					Data = authentication_text
				};

				// Verify the client against the event set.
				if (OnAuthenticationVerify != null) {
					OnAuthenticationVerify?.Invoke(this, auth_args);

					if (auth_args.Authenticated) {
						Info.Status = ClientStatus.Connected;
					}

				} else {
					Info.Status = ClientStatus.Connected;
				}

				if (Info.Status != ClientStatus.Connected || failure_reason != null) {
					Send("$" + nameof(OnAuthenticationFailure), failure_reason, true);
                    Disconnect("Authentication Failed. Reason: " + failure_reason);
					return false;

				} else {
					Send("$OnAuthenticationSuccess");
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
				cl.Send("$" + nameof(OnConnectedClientChange), new ClientInfo[] { Info });
			});

			Send("$" + nameof(OnConnectedClientChange), Server.Clients.Select(cl => cl.Value.Info).ToArray());
			logger.Debug("{0} CID {1}: Successfully authorized on the server.", Mode, Info.Id);

			return true;
		}

		protected virtual void RequestAuthentication() {
			try {
                WriteLine(JsonConvert.SerializeObject(Info));

				// Read the ID from the server
				string id;
				TryReadLine(out id, AUTH_TIMEOUT);

                int int_id;

				if (int.TryParse(id, out int_id) == false) {
					Disconnect("Server did not send a valid user id.", JsonRpcSource.Server);
					return;
				}

				Info.Id = int_id;

				// Authorize the client with the specified events.
				var auth_args = new ConnectorAuthenticationEventArgs();

				OnAuthenticationRequest?.Invoke(this, auth_args);

                WriteLine(auth_args.Data ?? "");

			} catch (OperationCanceledException) {
				Disconnect("Server did not provide connection information in a timely manor.", Mode);
			}
		}

        public void Connect() {


            try {
                // Start the writer.
                WriteLoop();

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
                //client_writer = new StreamWriter(base_stream, Encoding.UTF8, 1024 * 16, true);
                client_reader = new StreamReader(base_stream, Encoding.UTF8, true, 1024 * 16, true);

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
                    string method;
					string data;

                    try {
                        // See if we have reached the end of the stream.
                        if (TryReadLine(out method) == false) {
                            Disconnect("Connection closed", Mode);
                            return;
                        }
                    } catch (OperationCanceledException e) {
                        logger.Debug(e, "{0} CID {1}: Method reader was canceled", Mode, Info.Id);
                        return;
                    } catch (IOException e) {
                        logger.Error(e, "{0} CID {1}: IO Exception occurred while listening. Exception: {2}", Mode, Info.Id, e.InnerException.ToString());
                        Disconnect("Connection closed", Mode);
                        return;

                    } catch (AggregateException e) {
                        Exception base_exception = e.GetBaseException();

                        if (base_exception is IOException) {
                            if (client.Client.Connected) {
                                logger.Error(e, "{0} CID {1}: IO Exception occurred while listening. Exception: {2}", Mode, Info.Id, base_exception.ToString());
                            } else {
                                logger.Warn(e, "{0} CID {1}: Connection closed by the other party. Exception: {2}", Mode, Info.Id, base_exception.ToString());
                            }
                        }else if (base_exception is ObjectDisposedException) {
                            logger.Warn(e, "{0} CID {1}: Connection closed by the other party. Exception: {2}", Mode, Info.Id, base_exception.ToString());
                        } else {
                            logger.Error(e, "{0} CID {1}: Unknown exception occurred while listening. Exception: {2}", Mode, Info.Id, base_exception.ToString());
                        }

                        Disconnect("Connection closed", Mode);
                        return;
                    } catch (Exception e) {
                        logger.Error(e, "{0} CID {1}: Unknown exception occurred while listening. Exception: {2}", Mode, Info.Id, e.ToString());
                        Disconnect("Connection closed", Mode);
                        return;
                    }

                    if (method == null) {
                        Disconnect("Send invalid method", Mode);
                        return;
                    }

                    logger.Debug("{0} CID {1}: Method '{2}' called", Mode, Info.Id, method);

					

					if (TryReadLine(out data) == false) {
						Disconnect("Connection closed", Mode);
						return;
					}

					try {
                        if (method[0] == '$') {
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
			
		


		private void ExecuteSpecialAction(string method, string data) {
			ClientInfo[] clients_info;
			if (method == "$" + nameof(OnConnectedClientChange)) {
				clients_info = JsonConvert.DeserializeObject<ClientInfo[]>(data);
				OnConnectedClientChange?.Invoke(this, new ConnectedClientChangedEventArgs(clients_info));

			} else if (method == "$" + nameof(OnDisconnect)) {
				clients_info = JsonConvert.DeserializeObject<ClientInfo[]>(data);
				Disconnect(clients_info[0].DisconnectReason, (Mode == JsonRpcSource.Client) ? JsonRpcSource.Server : JsonRpcSource.Client);

			} else if (method == "$" + nameof(OnAuthenticationFailure)) {
				if (Mode == JsonRpcSource.Client) {
					logger.Debug("{0} CID {1}: Authorized", Mode, Info.Id);
					OnAuthenticationFailure?.Invoke(this, new AuthenticationFailureEventArgs(data));
				}

			} else if (method == "$OnAuthenticationSuccess") {
				// If this is the client, enable the ping timer.
				if (Mode == JsonRpcSource.Client) {
					ping_timer.Enabled = true;
				}

				OnConnect?.Invoke(this, new ClientConnectEventArgs<THandler>(Server, this));

			} else if (method == "$ping") {
				var source = JsonConvert.DeserializeObject<JsonRpcSource>(data);

				if (source == Mode) {
					ping_stopwatch.Stop();
					Ping = ping_stopwatch.ElapsedMilliseconds;
					Send("$ping-result", Ping);

					logger.Trace("{0} CID {1}: Ping {2}ms", Mode, Info.Id, Ping);

				} else {
					// Ping back immediately.
					Send("$ping", source);
				}

			} else if (method == "$ping-result") {
				var ping = JsonConvert.DeserializeObject<long>(data);
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
			if (Mode  == source && client.Client.Connected) {
				Send("$" + nameof(OnDisconnect), new ClientInfo[] { Info });
			}

			OnDisconnect?.Invoke(this, new ClientDisconnectEventArgs<THandler>(reason, source, Server, this, socket_error));
			cancellation_token_source?.Cancel();
			client?.Close();
            client_reader?.Dispose();
			base_stream?.Dispose();
			ping_timer?.Dispose();
			ping_stopwatch?.Stop();

			Info.Status = ClientStatus.Disconnected;
			logger.Info("{0} CID {1}: Stopped", Mode, Info.Id);
		}

        private void WriteLoop() {
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
        }

        private void WriteLine(string data) {
			logger.Trace("{0} CID {1}: Write line to stream: {2}", Mode, Info.Id, data);
			write_queue.Add(Encoding.UTF8.GetBytes(data + "\r\n"));
        }

		private bool TryReadLine(out string line, int timeout = -1) {
			var read_line_task = client_reader.ReadLineAsync();
			bool success = read_line_task.Wait(timeout, cancellation_token_source.Token);
			line = read_line_task.Result;

			logger.Trace("{0} CID {1}: Read line from stream: {2}", Mode, Info.Id, line);
            return success;
        }

		private void Send(string method, object json, bool force_send) {
			if (cancellation_token_source.IsCancellationRequested) {
				return;
			}

			if (client.Client.Connected == false) {
				//logger.Error("{0} CID {1}: Tried sending while the connection is closed.", Mode, Info.Id, method);
				return;
			}

			if (Info.Status == ClientStatus.Connecting && force_send == false) {
				throw new InvalidOperationException("Can not send request while still connecting.");
			}
			string json_text = JsonConvert.SerializeObject(json);

			logger.Trace("{0} CID {1}: Sending method '{2}' with data ", Mode, Info.Id, method, json_text);
			
			write_queue.Add(Encoding.UTF8.GetBytes(method + "\r\n" + ((json == null) ? "\r\n" : json_text + "\r\n")));
		}

		public void Send(string method, object json = null) {
			Send(method, json, false);
        }

		public void Dispose() {
			Disconnect("Class object disposed.", Mode);
		}
	}
}

