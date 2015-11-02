using DtronixJsonRpc;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using Xunit.Abstractions;

namespace DtronixJsonRpcTests {
	public class JsonRpcClientTests {
		private ITestOutputHelper output;

		public static int port = 2827;

		private ManualResetEvent wait = new ManualResetEvent(false);

		private TcpListener server;
		private JsonRpcClient<TestActionHandler> client;

		public JsonRpcClientTests(ITestOutputHelper output) {
			this.output = output;

			server = TcpListener.Create(Interlocked.Increment(ref port));
			server.Start();
			client = JsonRpcClient<TestActionHandler>.CreateClient("localhost", ((IPEndPoint)server.LocalEndpoint).Port, JsonRpcServerConfigurations.TransportMode.Bson);
		}


		[Fact]
		public async void Connect_should_connect() {

			var server_task = Task.Run(() => {
				var server_client = server.AcceptTcpClient();
				var server_client_stream = server_client.GetStream();
				Assert.NotNull(server_client_stream);
				
				server_client.Close();
				server.Stop();

				wait.Set();

			});

			await Task.Run(() => client.Connect()).ContinueWith(task => {
				Assert.Null(task.Exception);
			});

			Assert.True(wait.WaitOne(5000), "Did not activate reset event.");
		}

		[Fact]
		public async void Connect_should_authenticate() {


			var server_task = Task.Run(() => {
				var server_client = server.AcceptTcpClient();
				var server_client_stream = server_client.GetStream();
				

				Read().ToObject<JsonRpcParam<ClientInfo>>();

				server_client.Close();
				server.Stop();

				wait.Set();
			});

			await Task.Run(() => client.Connect()).ContinueWith(task => {
				Assert.Null(task.Exception);
			});

			Assert.True(wait.WaitOne(5000), "Did not activate reset event.");
		}

	}
}
