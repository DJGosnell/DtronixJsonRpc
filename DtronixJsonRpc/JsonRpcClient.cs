using Newtonsoft.Json;
using Newtonsoft.Json.Bson;
using Newtonsoft.Json.Linq;
using NLog;
using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using AsyncIO;

namespace DtronixJsonRpc {

	/// <summary>
	/// Client to connect and communicate on the JSON RPC protocol.
	/// </summary>
	/// <typeparam name="THandler">Action Handler to contain all action class instances.</typeparam>
	public class JsonRpcClient<THandler> : IDisposable
		where THandler : ActionHandler<THandler>, new() {

		private class ReturnResult {
			public ManualResetEventSlim reset_event;
			public JToken value;
		}

		private static Logger logger = LogManager.GetCurrentClassLogger();

		/// <summary>
		/// The time that an action will wait for a response from the server.
		/// </summary>
		public int RequestTimeout { get; set; } = 30 * 1000;

		/// <summary>
		/// The number of milliseconds elapsed to execute a command from the client to the server.
		/// </summary>
		public long Ping { get; private set; } = -1;

		/// <summary>
		/// Address this client is connecting from.
		/// </summary>
		public string Address { get; private set; }

		/// <summary>
		/// Port this client is bound to.
		/// 
		/// Default: 2828.
		/// </summary>
		public int Port { get; private set; } = 2828;

		/// <summary>
		/// Actions to execute on the connected client or server.
		/// </summary>
		public THandler Actions { get; }

		/// <summary>
		/// Object that is referenced by the action handlers.
		/// </summary>
		public object DataObject { get; set; }

		private long _RequestId = 0;

		/// <summary>
		/// Id number for sending requests to the server to allow for requests to be matched up to their responses.
		/// </summary>
		public long RequestId {
			get { return _RequestId; }
		}

		private readonly CancellationTokenSource cancellation_token_source = new CancellationTokenSource();
		// All streams and stream wrappers used in this client.
		private TcpClient client;
		private NetworkStream base_stream;
		private JsonReader reader;
		private JsonWriter writer;
		private StreamReader stream_reader;
		private StreamWriter stream_writer;
		private JsonSerializer serializer;
		private object write_lock = new object();
		private ConcurrentDictionary<string, ReturnResult> return_wait_results = new ConcurrentDictionary<string, ReturnResult>();

		internal Stopwatch ping_stopwatch;


		/// <summary>
		/// Creates a new instance of the JsonRpcClient class.
		/// </summary>
		/// <param name="address">Address the server is located.</param>
		/// <param name="port">Port the server is bound to.</param>
		/// <remarks>
		/// Use of this is not directly allowed because configuration is required before full construction can occur.
		/// </remarks>
		public JsonRpcClient(string address, int port = 2828) {
			serializer = new JsonSerializer();
			Address = address;
			Port = port;
		}

		/// <summary>
		/// Increments to the next request ID and returns it as a string.
		/// </summary>
		/// <returns>String representation of the request ID.</returns>
		public string GetNewRequestId() {
			return Interlocked.Increment(ref _RequestId).ToString();
		}

		/// <summary>
		/// Asynchronously waits for the result of the specified ID with an optional cancellation token.  Can only have one wait per ID.
		/// </summary>
		/// <typeparam name="T">Type to return the result as.</typeparam>
		/// <param name="id">ID of the request to wait on.</param
		/// <param name="token">Cancellation token for the awaiter.</param>
		/// <returns>Result of the request.</returns>
		public async Task<T> WaitForResult<T>(string id, CancellationToken token = default(CancellationToken)) {
			ReturnResult result;
			return default(T);
		}
				
		/// <summary>
		/// Connects to the server with the specified connection info.
		/// </summary>
		/// <returns>
		/// True on successful client connection, false if client did not fully connect. (Anonymous client, failed authentication, etc...)
		/// </returns>
		public bool Connect() {

			CompletionPort completionPort = CompletionPort.Create();

			bool exception = false;

			var task = Task.Factory.StartNew(() => {
				bool cancel = false;

				while (!cancel) {
					CompletionStatus[] completionStatuses = new CompletionStatus[10];

					int removed;

					completionPort.GetMultipleQueuedCompletionStatus(-1, completionStatuses, out removed);

					for (int i = 0; i < removed; i++) {
						if (completionStatuses[i].OperationType == OperationType.Signal) {
							cancel = true;
						} else if (completionStatuses[i].SocketError == SocketError.Success) {
							EventWaitHandle manualResetEvent = (EventWaitHandle)completionStatuses[i].State;
							manualResetEvent.Set();
						} else {
							exception = true;
						}
					}
				}
			});

			AutoResetEvent clientEvent = new AutoResetEvent(false);
			AutoResetEvent acceptedEvent = new AutoResetEvent(false);

			AsyncSocket listener = AsyncSocket.CreateIPv4Tcp();
			completionPort.AssociateSocket(listener, acceptedEvent);
			listener.Bind(IPAddress.Any, 0);
			int port = listener.LocalEndPoint.Port;
			listener.Listen(1);

			listener.Accept();

			AsyncSocket clientSocket = AsyncSocket.CreateIPv4Tcp();
			completionPort.AssociateSocket(clientSocket, clientEvent);
			clientSocket.Bind(IPAddress.Any, 0);
			clientSocket.Connect("localhost", port);

			clientEvent.WaitOne();
			acceptedEvent.WaitOne();

			var serverSocket = listener.GetAcceptedSocket();

			AutoResetEvent serverEvent = new AutoResetEvent(false);
			completionPort.AssociateSocket(serverSocket, serverEvent);

			byte[] recv = new byte[1];
			serverSocket.Receive(recv);

			byte[] data = new[] { (byte)1 };

			clientSocket.Send(data);
			clientEvent.WaitOne(); // wait for data to be send

			serverEvent.WaitOne(); // wait for data to be received

			Assert.AreEqual(1, recv[0]);

			completionPort.Signal(null);
			task.Wait();

			Assert.IsFalse(exception);

			completionPort.Dispose();
			listener.Dispose();
			serverSocket.Dispose();
			clientSocket.Dispose();


			return false;

		}


		/// <summary>
		/// Stops the connection and alerts the other party of this fact.
		/// </summary>
		/// <param name="reason">Reason this client is disconnecting.</param>
		public void Disconnect(string reason) {
			Disconnect(reason, SocketError.Success);
		}


		/// <summary>
		/// Stops the connection and alerts the other party of this face.
		/// </summary>
		/// <param name="reason">Reason this client is disconnecting.</param>
		/// <param name="source">Source of this disconnect request</param>
		/// <param name="socket_error"></param>
		private void Disconnect(string reason, SocketError socket_error) {
			if (string.IsNullOrWhiteSpace(reason)) {
				throw new ArgumentException("Reason for closing connection can not be null or empty.");
			}

			cancellation_token_source?.Cancel();


			// Close and dispose all streams.
			client?.Close();
			base_stream?.Dispose();
			stream_writer?.Dispose();
			stream_reader?.Dispose();
			writer?.Close();
			reader?.Close();
		}

		/// <summary>
		/// Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.
		/// </summary>
		public void Dispose() {
			Disconnect("Class object disposed.");
		}
	}
}

