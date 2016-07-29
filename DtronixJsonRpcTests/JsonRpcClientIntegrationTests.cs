using Newtonsoft.Json;
using Newtonsoft.Json.Bson;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using AsyncIO;
using DtronixJsonRpc.MessageQueue;
using Xunit;
using Xunit.Abstractions;

namespace DtronixJsonRpcTests {
	public class JsonRpcClientIntegrationTests {



		public JsonRpcClientIntegrationTests(ITestOutputHelper output) { }

		[Fact]
		public async void Client_connects_to_server() {
			var server = new MQServer(new MQServer.Config());
			server.Start();

			server.OnIncomingMessage += (sender, args) => {
				MQMessage message;
				while (args.Mailbox.TryDequeue(out message)) {
					var test = message[0].Data;
				}
			};

			Thread.Sleep(200);
			var client = new MQClient();


			Thread.Sleep(200);
			client.Send();
			client.Send();
			client.Send();
			client.Send();
			client.Send();



			Thread.Sleep(5000);
		}


		/*[Fact]
			public async void Ping_does_not_time_out() {
				CompletionPort completionPort = CompletionPort.Create();

				AutoResetEvent listenerEvent = new AutoResetEvent(false);
				AutoResetEvent clientEvent = new AutoResetEvent(false);
				AutoResetEvent serverEvent = new AutoResetEvent(false);

				completionPort.Signal();

				AsyncSocket listener = AsyncSocket.Create(AddressFamily.InterNetwork,
					SocketType.Stream, ProtocolType.Tcp);
				completionPort.AssociateSocket(listener, listenerEvent);

				AsyncSocket server = AsyncSocket.Create(AddressFamily.InterNetwork,
					SocketType.Stream, ProtocolType.Tcp);
				completionPort.AssociateSocket(server, serverEvent);

				AsyncSocket client = AsyncSocket.Create(AddressFamily.InterNetwork,
					SocketType.Stream, ProtocolType.Tcp);
				completionPort.AssociateSocket(client, clientEvent);

				Task.Factory.StartNew(() =>
				{
					CompletionStatus[] completionStatus = new CompletionStatus[40];

					while (true) {
						int removed;
						if (!completionPort.GetMultipleQueuedCompletionStatus(-1, completionStatus, out removed))
							continue;

						for (int i = 0; i < removed; i++) {
							try {


							} catch { }
						}

						/*Debug.WriteLine("{0} {1} {2}", completionStatus.SocketError,
							completionStatus.OperationType, completionStatus.BytesTransferred);

						if (completionStatus.State != null) {
							AutoResetEvent resetEvent = (AutoResetEvent)completionStatus.State;
							resetEvent.Set();
						}

		}
			});

			listener.Bind(IPAddress.Any, 5555);
			listener.Listen(1);

			client.Connect("localhost", 5555);

			listener.GetAcceptedSocket();


			byte[] sendBuffer = new byte[] { 2 };
			byte[] recvBuffer = new byte[1];

			client.Send(sendBuffer);
			server.Receive(recvBuffer);

			clientEvent.WaitOne();
			serverEvent.WaitOne();

			server.Dispose();
			client.Dispose();

		} */

		/*
		private void Loop() {
			var completionStatuses = new CompletionStatus[CompletionStatusArraySize];

			while (!m_stopping) {
				// Execute any due timers.
				int timeout = ExecuteTimers();

				int removed;

				if (!m_completionPort.GetMultipleQueuedCompletionStatus(timeout != 0 ? timeout : -1, completionStatuses, out removed))
					continue;

				for (int i = 0; i < removed; i++) {
					try {
						if (completionStatuses[i].OperationType == OperationType.Signal) {
							var mailbox = (IOThreadMailbox)completionStatuses[i].State;
							mailbox.RaiseEvent();
						}
						// if the state is null we just ignore the completion status
						else if (completionStatuses[i].State != null) {
							var item = (Item)completionStatuses[i].State;

							if (!item.Cancelled) {
								switch (completionStatuses[i].OperationType) {
									case OperationType.Accept:
									case OperationType.Receive:
										item.ProactorEvents.InCompleted(
											completionStatuses[i].SocketError,
											completionStatuses[i].BytesTransferred);
										break;
									case OperationType.Connect:
									case OperationType.Disconnect:
									case OperationType.Send:
										item.ProactorEvents.OutCompleted(
											completionStatuses[i].SocketError,
											completionStatuses[i].BytesTransferred);
										break;
									default:
										throw new ArgumentOutOfRangeException();
								}
							}
						}
					} catch (TerminatingException) { }
				}
			}
		}*/



	}
}
