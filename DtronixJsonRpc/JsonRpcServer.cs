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
    public class JsonRpcServer {

        private readonly CancellationTokenSource cancellation_token_source;
        private readonly TcpListener listener;

        private ConcurrentDictionary<int, JsonRpcConnector<ServerActions>> clients = new ConcurrentDictionary<int, JsonRpcConnector<ServerActions>>();

        private ServerActions server_actions;

        public string Address { get; set; }

        private string password;

        public ConcurrentDictionary<int, TheatreConnector<ServerActions>> Clients {
            get {
                return clients;
            }
        }

        private int last_client_id = 0;

        private Core core;

        public JsonRpcServer(Core core, string password) {
            this.core = core;
            this.password = password;
            cancellation_token_source = new CancellationTokenSource();
            server_actions = new ServerActions(this);
            listener = new TcpListener(IPAddress.Any, 2828);
        }


        public void Start() {

            listener.Start();

            Task.Factory.StartNew((main_task) => {
                while (cancellation_token_source.IsCancellationRequested == false) {
                    var client = listener.AcceptTcpClient();
                    var client_listener = TheatreConnector<ServerActions>.CreateServer(core, client, password, server_actions, last_client_id++);


                    client_listener.OnDisconnect += (sender, args) => {
                        TheatreConnector<ServerActions> removed_client;
                        clients.TryRemove(sender.Id, out removed_client);

                        Broadcast(cl => cl.Send(nameof(ClientActions.ClientDisconnect), new Tuple<int, string>(removed_client.Id, args.Reason)));
                    };

                    clients.TryAdd(client_listener.Id, client_listener);


                    client_listener.Start();

                }

            }, TaskCreationOptions.LongRunning, cancellation_token_source.Token).ContinueWith(task => {
                if (cancellation_token_source.IsCancellationRequested) {
                    Broadcast(cl => cl.Send(nameof(ClientActions.ClientDisconnect), "Server shutting down."));
                }
            }, TaskContinuationOptions.AttachedToParent);

        }

        public void Broadcast(Action<TheatreConnector<ServerActions>> action) {
            foreach (var client in clients) {
                if (client.Value.Connected == false) {
                    continue;
                }

                action(client.Value);
            }
        }

        private void Client_listener_OnDisconnect() {
            throw new NotImplementedException();
        }

        public void Stop(string reason) {
            Broadcast(cl => {
                cl.Stop("Server shutdown", JsonRpcMode.Server);
            });

            cancellation_token_source.Cancel();

            listener.Stop();
        }
    }
}
