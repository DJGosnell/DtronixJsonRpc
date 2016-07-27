using NetMQ;
using NetMQ.Sockets;
using NLog;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using NetMQ.Monitoring;

namespace DtronixJsonRpc {
	/// <summary>
	/// Server to listen and respond via the JSON RPC protocol.
	/// </summary>
	public class JsonRpcServer: IDisposable {

		private static Logger logger = LogManager.GetCurrentClassLogger();


		/// <summary>
		/// Gets the status of whether the server is actively listening for clients or not.
		/// </summary>
		public bool IsRunning { get; private set; }

		/// <summary>
		/// Object that is referenced by the action handlers.
		/// </summary>
		public object DataObject { get; set; }

		/// <summary>
		/// Stored reason why the server was stopped.
		/// </summary>
		public string StopReason { get; private set; }

		/// <summary>
		/// Current configuration of the server.
		/// </summary>
		public JsonRpcServerConfigurations Configurations { get; }

		private RouterSocket public_router;

		private RouterSocket worker_router;

		private List<string> worker_ids = new List<string>();

		private bool _IsStopping = false;
		/// <summary>
		/// Gets whether or not the server is in the process of stopping or not.
		/// </summary>
		public bool IsStopping {
			get { return _IsStopping; }
		}

		private readonly CancellationTokenSource cancellation_token_source;
		private NetMQPoller poller;
		private NetMQMonitor monitor;
		private NetMQTimer ping_timer;

		private SortedList<byte[], Client> Clients { get; } = new SortedList<byte[], Client>(new ByteComparer());

		private class Client {
			public int LastResponded { get; set; }
		}

		/// <summary>
		/// Creates a JsonRpcServer with default configurations.
		/// </summary>
		public JsonRpcServer() : this(new JsonRpcServerConfigurations()) { }


		/// <summary>
		/// Creates a JsonRpcServer instance with the specified configurations
		/// </summary>
		/// <param name="configurations">Configurations to load on server initialization.</param>
		public JsonRpcServer(JsonRpcServerConfigurations configurations) {
			Configurations = configurations;
			cancellation_token_source = new CancellationTokenSource();
			public_router = new RouterSocket();
			worker_router = new RouterSocket();
		}


		/// <summary>
		/// Starts listening for incoming clients.  Will listen on the same thread as the start method is called on.
		/// </summary>
		/// <returns>
		/// Returns true on successful start.
		/// </returns>
		public bool Start() {

			if (IsRunning) {
				throw new InvalidOperationException("Server is already running.");
			}
			IsRunning = true;

			logger.Info("Server: Starting");

			logger.Debug("Server: Listening for connections.");

			
			public_router.Bind(Configurations.BindingAddress);

			worker_router.Bind("inproc://worker-router");
			ping_timer = new NetMQTimer(Configurations.PingFrequency);
			poller = new NetMQPoller {public_router, worker_router};
			monitor = new NetMQMonitor(public_router, "inproc://public-router-monitor", SocketEvents.Connected);
			public_router.ReceiveReady += Public_router_ReceiveReady;
			worker_router.ReceiveReady += Worker_router_ReceiveReady;
			ping_timer.Elapsed += Ping_timer_Elapsed;
			monitor.Connected += Monitor_Connected;

			//monitor.Accepted += Monitor_Accepted;



			poller.RunAsync();

			return true;
		}

		private void Monitor_Connected(object sender, NetMQMonitorSocketEventArgs e) {
			
		}

		private void Monitor_Accepted(object sender, NetMQMonitorSocketEventArgs e) {
			
		}

		private void Ping_timer_Elapsed(object sender, NetMQTimerEventArgs e) {
			var interval = e.Timer.Interval;
			var clients = Clients.ToList();

			foreach (var client in clients) {
				client.Value.LastResponded += interval;

				// If the client is over the disconnection limit, remove it.
				if (client.Value.LastResponded > Configurations.PingTimeoutDisconnectTime) {

					Clients.Remove(client.Key);
				}
			}
		}

		private void Ping() {
			
		}

		private void Public_router_ReceiveReady(object sender, NetMQSocketEventArgs e) {
			var message = new NetMQMessage();
			for (var i = 0; i < 100; i++) {

				if (!e.Socket.TryReceiveMultipartMessage(ref message, 5)) {
					break;
				}

				byte[] client_id = message[0].ToByteArray();

				//e.Socket.

				//Clients.Add(client_id, new Client {Socket = e.Socket});

				//SortedList<byte[], Client> myList = 

				//myList.con

			}

		}

		private void Worker_router_ReceiveReady(object sender, NetMQSocketEventArgs e) {
			var message = new NetMQMessage();
			for (var i = 0; i < 100; i++) {

				if (!e.Socket.TryReceiveMultipartMessage(ref message, 5)) {
					break;
				}

				

			}
		}


		/// <summary>
		/// Initiates a stop command for the server.
		/// </summary>
		/// <param name="reason">A reason to log and broadcast for the server shutdown.</param>
		public void Stop(string reason) {
			if (StopReason != null) {
				logger.Debug("Server: Stop requested after server is already in the process of stopping. Reason: {0}", reason);
			}

			StopReason = reason;

			logger.Info("Server: Stopping server. Reason: {0}", reason);
			worker_router.Disconnect("inproc://worker-router");
			public_router.Disconnect(Configurations.BindingAddress);
			poller.Dispose();

			
			IsRunning = false;
		}

		public void Dispose() {
			NetMQConfig.Cleanup();
			Stop("Server class instance disposed");
		}


	}
}
