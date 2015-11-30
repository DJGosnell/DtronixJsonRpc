using DtronixJsonRpc;
using DtronixJsonRpcTests.Actions;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Xunit.Abstractions;

namespace DtronixJsonRpcTests {
	public class JsonRpcIntegrationTestBase {
		public static int port = 2828;

		protected ManualResetEvent wait = new ManualResetEvent(false);

		protected JsonRpcServer<TestActionHandler> server;
		protected JsonRpcClient<TestActionHandler> client;
		protected JsonSerializer serializer = new JsonSerializer();
		protected Task server_task;
		protected Task client_task;

		protected const string AUTH_TEXT = "ArbitraryAuthText";
		protected ITestOutputHelper output;



		public JsonRpcIntegrationTestBase(ITestOutputHelper output) {
			this.output = output;

			var configs = new JsonRpcServerConfigurations() {
				BindingPort = port, //Interlocked.Increment(ref port),
				TransportProtocol = JsonRpcServerConfigurations.TransportMode.Bson
			};

			server = new JsonRpcServer<TestActionHandler>(configs);

			client = JsonRpcClient<TestActionHandler>.CreateClient("localhost", server.Configurations.BindingPort, JsonRpcServerConfigurations.TransportMode.Bson);
			client.Info.Username = "TestUser";

			client.OnAuthenticationRequest += (sender, e) => {
				e.Data = AUTH_TEXT;
			};

			server.OnAuthenticationVerification += (sender, e) => {
				e.Authenticated = true;
			};
		}

		protected void CompleteTest() {
			server.Stop("Test completed successfully.");
		}

		protected void FailTest() {
			server.Stop("Test failed.");
		}


		protected async Task<bool> StartAndWaitClient(int wait_duration = 5000) {
			bool ex_thrown = false;

			try {
				server_task.Start();
				client_task.Start();

				if (await Task.WhenAny(server_task, Task.Delay(wait_duration)) != server_task) {
					client.Disconnect("Test failed to complete within the time limitation.");
					ex_thrown = true;
					throw new TimeoutException("Test failed to complete within the time limitation.");
				}

			} finally {
				server_task.Exception?.Handle(ex => {
					throw ex;
				});

				client_task.Exception?.Handle(ex => {
					throw ex;
				});

				if (ex_thrown == false && server.StopReason != "Test completed successfully.") {
					throw new Exception("Test did not complete successfully.");
				}
			}

			return true;
		}
	}
}
