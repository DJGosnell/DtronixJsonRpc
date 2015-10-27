using DtronixJsonRpc;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Xunit;
using Xunit.Abstractions;

namespace DtronixJsonRpcTests {

	public class ServerTests : BaseTest {

		public ServerTests(ITestOutputHelper output) : base(output) {

		}

		[Fact]
		public void StartsStops() {
			var server_start_reset = AddWait("Server start");
			var server_stopped_reset = AddWait("Server stop");

			Server.OnStart += (sender, e) => {
				server_start_reset.Set();
				Server.Stop("Test completed");
			};

			Server.OnStop += (sender, e) => {
				server_stopped_reset.Set();
			};


			StartServerConnectClient();

		}


		[Fact]
		public void CallsClientMethod() {
			var called_method_reset = AddWait("Method call");


			var random_long = 1684584139;

			Client.OnConnect += (sender, e) => {
				Server.Clients[0].Actions.TestClientActions.Test(new TestClientActions.TestArgs(){
					RandomLong = random_long
				});
			};

			Client.Actions.TestClientActions.MethodCalled += (sender, e) => {
				if (e.Type == typeof(TestClientActions)) {
					if (e.Method == "Test") {
						Assert.Equal(random_long, ((TestClientActions.TestArgs)e.Arguments).RandomLong);
						called_method_reset.Set();
						Client.Disconnect("Test completed", JsonRpcSource.Client);
					}
				}

			};

			Server.OnClientDisconnect += (sender, e) => {
				Server.Stop("Test completed");
			};

			StartServerConnectClient();

		}

		[Fact]
		public void RegistersClientDisconnection() {

			Client.OnConnect += (sender, e) => {
				e.Client.Disconnect("Test disconnection");
			};

			Server.OnClientDisconnect += (sender, e) => {
				Server.Stop("Test completed");
			};

			StartServerConnectClient();
		}

		[Fact]
		public void ClientDisconnectionRemovesFromServerActiveList() {

			Client.OnConnect += (sender, e) => {
				e.Client.Disconnect("Test disconnection");
			};

			Server.OnClientDisconnect += (sender, e) => {
				Assert.Equal(0, e.Server.Clients.Count);
				Server.Stop("Test completed");
			};

			StartServerConnectClient();
		}
	}
}
