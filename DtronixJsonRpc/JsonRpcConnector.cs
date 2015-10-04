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

namespace DtronixJsonRpc {
	public class JsonRpcConnector<THandler> : IDisposable
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

		public THandler Actions { get; }

		private object lock_object = new object();

		public event EventHandler<JsonRpcConnector<THandler>, ClientDisconnectEventArgs<THandler>> OnDisconnect;
		public event EventHandler<JsonRpcConnector<THandler>, ClientConnectEventArgs<THandler>> OnConnect;
		public event EventHandler<JsonRpcConnector<THandler>, ConnectorAuthenticationEventArgs> OnAuthorizationRequest;
		public event EventHandler<JsonRpcConnector<THandler>> OnAuthorizationFailure;
		public event EventHandler<JsonRpcConnector<THandler>, ConnectorAuthenticationEventArgs> OnAuthorizationVerify;
		public event EventHandler<JsonRpcConnector<THandler>, ConnectedClientChangedEventArgs> OnConnectedClientChange;

		public JsonRpcSource Mode { get; protected set; }

		public JsonRpcServer<THandler> Server { get; private set; }

		private const int AUTH_TIMEOUT = 2000;

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

		protected virtual bool AuthorizeClient() {
			// Read the initial user info.
			

			try {
				var user_info_text_task = client_reader.ReadLineAsync();
				user_info_text_task.Wait(10000, cancellation_token_source.Token);

				var user_info = JsonConvert.DeserializeObject<ClientInfo>(user_info_text_task.Result);

				if (user_info == null) {
					Disconnect("User information passed was invalid.", Mode);
					return false;
				}

				Info.Username = user_info.Username.Trim();

				// Send the ID to the client
				client_writer.WriteLineAsync(Info.Id.ToString()).Wait(AUTH_TIMEOUT, cancellation_token_source.Token);
				client_writer.FlushAsync().Wait(AUTH_TIMEOUT, cancellation_token_source.Token);

				// Checks to ensure the client should connect.
				if (Server.Clients.Values.FirstOrDefault(cli => cli.Info.Id != Info.Id && cli.Info.Username == Info.Username) != null) {
					Disconnect("Duplicate username on server.", JsonRpcSource.Server);
					return false;
				}

				// Authorize client.
				var auth_args = new ConnectorAuthenticationEventArgs();

				var authorization_text_task = client_reader.ReadLineAsync();
				authorization_text_task.Wait(10000, cancellation_token_source.Token);

				auth_args.Data = authorization_text_task.Result;

				// Verify the client against the event set.
				if (OnAuthorizationVerify != null) {
					OnAuthorizationVerify?.Invoke(this, auth_args);

					if (auth_args.Authenticated) {
						Info.Status = ClientStatus.Connected;
					} else {
						client_writer.WriteLineAsync("0").Wait(AUTH_TIMEOUT, cancellation_token_source.Token);
						client_writer.FlushAsync().Wait(AUTH_TIMEOUT, cancellation_token_source.Token);

						Disconnect("Authentication failed.", JsonRpcSource.Server);

						return false;
					}

				} else {
					Info.Status = ClientStatus.Connected;
				}

				// Alert the client on a successful authentication.
				client_writer.WriteLineAsync("1").Wait(AUTH_TIMEOUT, cancellation_token_source.Token);
				client_writer.FlushAsync().Wait(AUTH_TIMEOUT, cancellation_token_source.Token);

			} catch (OperationCanceledException) {
				Disconnect("Client did not provide connection information in a timely mannor.", Mode);
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

		protected virtual bool RequestAuthorization() {
			try {
				client_writer.WriteLineAsync(JsonConvert.SerializeObject(Info)).Wait(AUTH_TIMEOUT, cancellation_token_source.Token);
				client_writer.FlushAsync().Wait(AUTH_TIMEOUT, cancellation_token_source.Token);

				// Read the ID from the server
				var id_task = client_reader.ReadLineAsync();
				id_task.Wait(AUTH_TIMEOUT, cancellation_token_source.Token);

				int int_id;

				if (int.TryParse(id_task.Result, out int_id) == false) {
					Disconnect("Server did not send a valid user id.", JsonRpcSource.Server);
				}

				Info.Id = int_id;

				// Authorize the client with the specified events.
				var auth_args = new ConnectorAuthenticationEventArgs();

				OnAuthorizationRequest?.Invoke(this, auth_args);

				client_writer.WriteLineAsync(auth_args.Data ?? "").Wait(AUTH_TIMEOUT, cancellation_token_source.Token);
				client_writer.FlushAsync().Wait(AUTH_TIMEOUT, cancellation_token_source.Token);

				var auth_result_task = client_reader.ReadLineAsync();
				auth_result_task.Wait(AUTH_TIMEOUT, cancellation_token_source.Token);

				return auth_result_task.Result == "1";

			} catch (OperationCanceledException) {
				Disconnect("Server did not provide connection information in a timely mannor.", Mode);
				return false;
			}
		}

		public void Connect() {

#pragma warning disable 4014
			Task.Factory.StartNew(ConnectionHandler, TaskCreationOptions.LongRunning, cancellation_token_source.Token).ContinueWith(task => {
				if (Info.Status != ClientStatus.Disconnected || Info.Status == ClientStatus.Disconnecting) {
					Disconnect("Client closed", Mode);
				} else {
					if (task.Exception != null) {
						try {
							var base_exception = task.Exception.GetBaseException();

							if (base_exception is SocketException) {
								var socket_exception = base_exception as SocketException;
								Disconnect("Server connection issues", JsonRpcSource.Client, socket_exception.SocketErrorCode);

							} else {
								logger.Error(base_exception, "{0} CID {1}: Exception Occurred", Mode, Info.Id);
							}

						} catch (Exception ex) {
							logger.Error(ex, "{0} CID {1}: Exception occurred while disconnecting.", Mode, Info.Id);
							throw;
						}
					}
				}

			}, TaskContinuationOptions.AttachedToParent);
		}

		private void ConnectionHandler(object main_task) {

			//try {
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
			client_writer = new StreamWriter(base_stream, Encoding.UTF8, 1024 * 16, true);
			client_reader = new StreamReader(base_stream, Encoding.UTF8, true, 1024 * 16, true);

			logger.Debug("{0} CID {1}: Connected. Authorizing...", Mode, Info.Id);
			if (Mode == JsonRpcSource.Server) {
				if(AuthorizeClient() == false) {
					return;
				}
			} else {
				if (RequestAuthorization() == false) {
					OnAuthorizationFailure.Invoke(this, this);
                    return;
				}
			}

			logger.Debug("{0} CID {1}: Authorized", Mode, Info.Id);

			Info.Status = ClientStatus.Connected;

			OnConnect?.Invoke(this, new ClientConnectEventArgs<THandler>(Server, this));
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
				} catch (OperationCanceledException e) {
					logger.Debug(e, "{0} CID {1}: Method reader was canceled", Mode, Info.Id);
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

				logger.Debug("{0} CID {1}: Method '{2}' called", Mode, Info.Id, method);

				var json_data = JsonConvert.DeserializeObject(client_reader.ReadLine(), cli_type);

				try {
					if (method[0] == '$') {
						ExecuteSpecialAction(method, json_data);
					} else {
						Actions.ExecuteAction(method, json_data);
					}
				} catch (Exception e) {
					logger.Error(e, "{0} CID {1}: Called action threw exception", Mode, Info.Id);
				}
			}
		}

		private void ExecuteSpecialAction(string method, object json_data) {
			switch (method) {
				case "$" + nameof(OnConnectedClientChange):
					OnConnectedClientChange?.Invoke(this, new ConnectedClientChangedEventArgs(json_data as ClientInfo[]));
					break;

				case "$" + nameof(OnDisconnect):
					var ci = json_data as ClientInfo[];
					Disconnect(ci[0].DisconnectReason, (Mode == JsonRpcSource.Client) ? JsonRpcSource.Server : JsonRpcSource.Client);
					break;
			}
		}

		public void Disconnect(string reason, JsonRpcSource source, SocketError socket_error = SocketError.Success) {
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
			if (Mode  == source) {
				Send("$OnDisconnect", new ClientInfo[] { Info });
			}

			OnDisconnect?.Invoke(this, new ClientDisconnectEventArgs<THandler>(reason, source, Server, this, socket_error));
			cancellation_token_source.Cancel();
			client.Close();
            client_reader.Dispose();
			client_writer.Dispose();
			base_stream.Dispose();

			Info.Status = ClientStatus.Disconnected;
			logger.Info("{0} CID {1}: Stopped", Mode, Info.Id);
		}


		public void Send(string method, object json = null) {
			if (cancellation_token_source.IsCancellationRequested) {
				return;
			}

			if (Info.Status == ClientStatus.Connecting) {
				throw new InvalidOperationException("Can not send request while still connecting.");
			}

			logger.Debug("{0} CID {1}: Sending method '{2}'", Mode, Info.Id, method);
			try {
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
						logger.Warn("{0} CID {1}: Tried to write to the stream when the client was closed.", Mode, Info.Id);
						// The client was closed.  
						return;
					}

				}
			} catch (IOException e) {
				logger.Error(e, "Exception occured when trying to write to the stream.");
				Disconnect("Writing error to stream.", Mode);
			}

		
		}

		public void Dispose() {
			Disconnect("Class object disposed.", Mode);
		}
	}
}
