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

			server.OnStop += (sender, e) => {
				wait.Set();
			};
		}

		protected void CompleteTest() {
			server.Stop("Test completed successfully.");
			wait.Set();
		}

		protected void FailTest() {
			server.Stop("Test failed.");
			wait.Set();
		}


		protected async Task<bool> StartAndWaitClient(int wait_duration = 5000) {
			if (await Task.Run(() => wait.WaitOne(5000)) == false) {
				client.Disconnect("Test failed to complete within the time limitation.");
				server.Stop("Test failed to complete within the time limitation.");
				throw new TimeoutException("Test failed to complete within the time limitation.");
			}

			if (server.StopReason != "Test completed successfully.") {
				throw new Exception("Test did not complete successfully.");
			}

			return true;
		}
	}
}
