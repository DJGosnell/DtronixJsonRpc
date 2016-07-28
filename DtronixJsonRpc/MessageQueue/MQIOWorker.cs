using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using AsyncIO;

namespace DtronixJsonRpc.MessageQueue {
	public class MQIOWorker : IDisposable {

		public class WorkerEventArgs : EventArgs {
			public MQIOWorker Worker { get; }
			public CompletionStatus Status { get; }

			public WorkerEventArgs(MQIOWorker worker, CompletionStatus status) {
				Worker = worker;
				Status = status;
			}

		}

		private Thread worker_thread;
		private CompletionPort completion_port;

		private long average_idle_time = -1;
		private Stopwatch idle_stopwatch = new Stopwatch();
		private bool busy;

		public bool Busy => busy;

		public event EventHandler<WorkerEventArgs> OnSend;
		public event EventHandler<WorkerEventArgs> OnReceive;
		public event EventHandler<WorkerEventArgs> OnConnect;
		public event EventHandler<WorkerEventArgs> OnDisconnect;
		public event EventHandler<WorkerEventArgs> OnSignal;

		// TODO: Average idle time

		public MQIOWorker(CompletionPort completion_port) {
			this.completion_port = completion_port;
			worker_thread = new Thread(Listen) {
				IsBackground = true,
				Name = "queue-worker"
			};

			worker_thread.Start();
		}

		private void Listen() {
			bool cancel = false;

			while (!cancel) {
				CompletionStatus completion_status;
				idle_stopwatch.Restart();

				// Check the completion port status
				if (completion_port.GetQueuedCompletionStatus(-1, out completion_status) == false) {
					continue;
				}

				// Check the average time this thread remains idle
				average_idle_time = average_idle_time == -1
					? idle_stopwatch.ElapsedMilliseconds
					: (idle_stopwatch.ElapsedMilliseconds + average_idle_time)/2;


				busy = true;
				switch (completion_status.OperationType) {
					case OperationType.Send:
						OnSend?.Invoke(this, new WorkerEventArgs(this, completion_status));
						break;
					case OperationType.Receive:
						OnReceive?.Invoke(this, new WorkerEventArgs(this, completion_status));


						break;
					case OperationType.Connect:
						OnConnect?.Invoke(this, new WorkerEventArgs(this, completion_status));
						break;
					case OperationType.Disconnect:
						OnDisconnect?.Invoke(this, new WorkerEventArgs(this, completion_status));
						cancel = true;
						break;
					case OperationType.Signal:
						OnSignal?.Invoke(this, new WorkerEventArgs(this, completion_status));
						break;
				}

				busy = false;

			}
		}

		public void Dispose() {
			if (worker_thread.IsAlive) {
				worker_thread.Abort();
			}
		}
	}
}
