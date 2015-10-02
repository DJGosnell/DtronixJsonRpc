using System;
using Xunit;
using DtronixJsonRpc;
using Xunit.Abstractions;
using System.Threading.Tasks;
using System.Threading;

namespace DtronixJsonRpcTests {
	public class ClientServerInteractionTests {
		private readonly ITestOutputHelper output;

		private JsonRpcServer<TestActionHandler> Server { get; }
		private JsonRpcConnector<TestActionHandler> Client { get; }

		public ClientServerInteractionTests(ITestOutputHelper output) {
			this.output = output;
			Server = new JsonRpcServer<TestActionHandler>(null);
			Client = new JsonRpcConnector<TestActionHandler>("localhost");
			Client.Info.Username = "DefaultTestClient";
		}

		[Fact]
		public void ServerStartsAndClientConnects() {
			var called_method_reset = new ManualResetEvent(false);
			Server.Start();

			Client.OnConnect += (sender, e) => {
				called_method_reset.Set();
			};
			
			Client.Connect();

			Assert.True(called_method_reset.WaitOne(5000));

			Server.Stop("Test completed");
		}


		[Fact]
		public void ServerCallesClientMethod() {
			var called_method_reset = new ManualResetEvent(false);
			Server.Start();

			Client.OnConnect += (sender, e) => {
				e.Server.Broadcast(cl => cl.Actions.TestClientActions.Test(new TestClientActions<TestActionHandler>.TestClientActionTestArgs() {
					RandomInt = 1516736
				}));
			};

			Client.Actions.TestClientActions.MethodCalled += (sender, e) => {
				if(e.Type == typeof(TestClientActions<TestActionHandler>)) {
					if (e.Method == "Test") {
						called_method_reset.Set();
					}
				}
			
			};


			Client.Connect();

			Assert.True(called_method_reset.WaitOne(5000));

			Server.Stop("Test completed");


		}
	}
}
