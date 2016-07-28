using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using AsyncIO;
using NLog;

namespace DtronixJsonRpc.MessageQueue {
	public class MQServer : IDisposable {
		private static readonly Logger logger = LogManager.GetCurrentClassLogger();

		public class Client {
			public Guid Id { get; set; }
			public AsyncSocket Socket { get; set; }

		}


		public class Config {
			public int MinimumWorkers { get; set; } = 4;

			/// <summary>
			/// Maximum backlog for pending connections.
			/// The default value is 100.
			/// </summary>
			public int ListenerBacklog { get; set; } = 100;
		}

		private readonly Config configurations;

		private readonly CompletionPort listen_completion_port;
		internal CompletionPort worker_completion_port;
		private readonly AsyncSocket listener;
		private readonly Thread listen_thread;

		private bool is_running;

		private readonly Dictionary<Guid, Client> connected_clients = new Dictionary<Guid, Client>();
		private readonly List<MQIOWorker> workers = new List<MQIOWorker>();


		public MQServer(Config configurations) {
			this.configurations = configurations;
			listen_completion_port = CompletionPort.Create();
			worker_completion_port = CompletionPort.Create();

			listener = AsyncSocket.Create(AddressFamily.InterNetwork,
				SocketType.Stream, ProtocolType.Tcp);

			listen_completion_port.AssociateSocket(listener, listener);

			listen_thread = new Thread(Listen) {
				IsBackground = true,
				Name = "queue-server-connection-listener"
			};

			for (var i = 0; i < this.configurations.MinimumWorkers; i++) {
				workers.Add(new MQIOWorker(worker_completion_port));
			}

			listener.Bind(new IPEndPoint(IPAddress.Any, 2828));

			

		}

		public void Start() {
			if (is_running) {
				throw new InvalidOperationException("Server is already running.");
			}

			listener.Listen(configurations.ListenerBacklog);
			listen_thread.Start();

			listener.Accept();
			//var socket = listener.GetAcceptedSocket();
		}

		public void Stop() {
			if (is_running == false) {
				throw new InvalidOperationException("Server is not running.");
			}

			listen_completion_port.Signal(null);

			is_running = false;
		}


		private void Listen() {
			is_running = true;
			var cancel = false;

			while (!cancel) {
				CompletionStatus completion_status;

				if (listen_completion_port.GetQueuedCompletionStatus(5000, out completion_status) == false) {
					continue;
				}

				switch (completion_status.OperationType) {
					case OperationType.Accept:
						var socket = completion_status.AsyncSocket.GetAcceptedSocket();
						var guid = new Guid();
						var client_cl = new Client {
							Id = guid,
							Socket = socket
						};

						connected_clients.Add(guid, client_cl);

						// Add the new socket to the worker completion port.
						worker_completion_port.AssociateSocket(socket, client_cl);

						// Signal a worker that the new client has connected and to handle the work.
						worker_completion_port.Signal(client_cl);

						break;

					case OperationType.Connect:
						break;

					case OperationType.Disconnect:
						break;

					case OperationType.Signal:
						var state = completion_status.State as Client;
						if (state != null) {
							// If the state is another socket, this is a disconnected client that needs to be removed from the list.
							var client = state;

							if (connected_clients.Remove(client.Id) == false) {
								logger.Error("Client {0} was not able to be removed from the list of active clients", client.Id);
							}

						} else if (completion_status.State == null) {
							cancel = true;
							is_running = false;
						}

						break;
				}
			}
		}
		

		public void Dispose() {
			if (is_running) {
				Stop();
			}

			listen_completion_port.Dispose();
			worker_completion_port.Dispose();


			foreach (var worker in workers) {
				worker.Dispose();
			}
		}
	}

}
