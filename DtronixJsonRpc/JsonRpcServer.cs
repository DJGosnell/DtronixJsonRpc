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
    public class JsonRpcServer<THandler> 
		where THandler : ActionHandler<THandler>, new() {

		private static Logger logger = LogManager.GetCurrentClassLogger();

		private readonly CancellationTokenSource cancellation_token_source;
        private readonly TcpListener listener;

        private ConcurrentDictionary<int, JsonRpcConnector<THandler>> clients = new ConcurrentDictionary<int, JsonRpcConnector<THandler>>();

        public string Address { get; set; }

        private string token;

        public ConcurrentDictionary<int, JsonRpcConnector<THandler>> Clients {
            get {
                return clients;
            }
        }

        private int last_client_id = 0;

        public JsonRpcServer(string password) {
            token = password;
            cancellation_token_source = new CancellationTokenSource();
            listener = new TcpListener(IPAddress.Any, 2828);
        }


        public void Start() {
			listener.Start();

			Task.Factory.StartNew((main_task) => {
				logger.Debug("Server: Listening for connections.");
				while (cancellation_token_source.IsCancellationRequested == false) {
					
					var client = listener.AcceptTcpClientAsync();
					logger.Debug("Server: New client attempting to connect");
					try {
						client.Wait();

					} catch (Exception e) {
						logger.Info(e, "Server: Stopped listening for clients.", null);
					}

					var client_listener = new JsonRpcConnector<THandler>(this, client.Result, last_client_id++);

                    client_listener.OnDisconnect += (sender, args) => {
						logger.Info("Server: Client ({0}) disconnected. Reason: {0}", sender.Info.Id, args.Reason);
						JsonRpcConnector<THandler> removed_client;
                        clients.TryRemove(sender.Info.Id, out removed_client);

                        Broadcast(cl => cl.Send("$" + nameof(JsonRpcConnector<THandler>.OnConnectedClientChange), new ClientInfo[] { removed_client.Info }));
                    };

                    clients.TryAdd(client_listener.Info.Id, client_listener);

                    client_listener.Connect();

                }

            }, TaskCreationOptions.LongRunning, cancellation_token_source.Token).ContinueWith(task => {
                if (cancellation_token_source.IsCancellationRequested) {
                    Broadcast(cl => cl.Send("$" + nameof(JsonRpcConnector<THandler>.OnDisconnect), "Server shutting down."));
                }
            }, TaskContinuationOptions.AttachedToParent);

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
			if (cancellation_token_source.IsCancellationRequested) {
				logger.Debug("Server: Stop requested after server is already in the process of stopping. Reason: {0}", reason);
			}
			logger.Info("Server: Stopping server. Reason: {0}", reason);
			Broadcast(cl => {
                cl.Disconnect("Server shutdown", JsonRpcSource.Server);
            });

            cancellation_token_source.Cancel();

            listener.Stop();

        }
    }
}
