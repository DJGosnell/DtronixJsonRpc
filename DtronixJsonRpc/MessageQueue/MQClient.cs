using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using AsyncIO;
using NLog;

namespace DtronixJsonRpc.MessageQueue {
	public class MQClient {
		private static readonly Logger logger = LogManager.GetCurrentClassLogger();

		private readonly List<MQIOWorker> workers = new List<MQIOWorker>();

		private CompletionPort worker_completion_port;
		private AsyncSocket client_socket;



		public MQClient() {
			worker_completion_port = CompletionPort.Create();

			client_socket = AsyncSocket.Create(AddressFamily.InterNetwork,
				SocketType.Stream, ProtocolType.Tcp);

			worker_completion_port.AssociateSocket(client_socket);

			

			for (var i = 0; i < 4; i++) {
				var worker = new MQIOWorker(worker_completion_port);
				worker.OnConnect += Worker_OnConnect;
				workers.Add(worker);
			}


			client_socket.Connect(IPAddress.Parse("127.0.0.1"), 2828);



		}

		private void Worker_OnConnect(object sender, MQIOWorker.WorkerEventArgs e) {
			/*var bytes = new List<byte>();
			bytes.Add(1);
			bytes.AddRange(BitConverter.GetBytes(1));
			bytes.Add(236);
			e.Status.AsyncSocket.Send(bytes.ToArray(), 0, bytes.Count, SocketFlags.None);*/
		}

		public void Send() {
			var bytes = new List<byte>();
			bytes.Add(1);
			bytes.AddRange(BitConverter.GetBytes(1));
			bytes.Add(236);

			client_socket.Send(bytes.ToArray(), 0, bytes.Count, SocketFlags.None);
			client_socket.Send(bytes.ToArray(), 0, bytes.Count, SocketFlags.None);
			bytes[0] = 2;
			client_socket.Send(bytes.ToArray(), 0, bytes.Count, SocketFlags.None);
			//client_socket.Send(new byte[] { 4, 5, 6, 7 }, 0, 4, SocketFlags.None);
		}
	}
}
