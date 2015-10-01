using System;
using Xunit;
using DtronixJsonRpc;
using Xunit.Abstractions;
using System.Threading.Tasks;
using System.Threading;

namespace DtronixJsonRpcTests {
	public class ClientServerInteractionTests {
		private readonly ITestOutputHelper output;

		public ClientServerInteractionTests(ITestOutputHelper output) {
			this.output = output;
		}


		[Fact]
		public void ServerStartsAndClientConnects() {
			var called_method_reset = new ManualResetEvent(false);



			var server = new JsonRpcServer<TestActionHandler>(null);

			server.Start();



			var client = new JsonRpcConnector<TestActionHandler>("localhost");



            client.Info.Username = "TestUsername";

			client.OnConnect += (sender, e) => {
				server.Broadcast(cl => cl.Actions.TestClientActions.Test(new TestClientActions<TestActionHandler>.TestClientActionTestArgs() {
					RandomInt = 1516736
				}));
			};

			client.Actions.TestClientActions.MethodCalled += (sender, e) => {
				if(e.Type == typeof(TestClientActions<TestActionHandler>)) {
					if (e.Method == "Test") {
						called_method_reset.Set();
					}
				}
			
			};


			client.Connect();

			Assert.True(called_method_reset.WaitOne(5000));

			server.Stop("Test completed");


		}

		private void sender(TestClientActions<TestActionHandler> sender, TestClientMethodCalledEventArgs e) {
			throw new NotImplementedException();
		}
	}
}
