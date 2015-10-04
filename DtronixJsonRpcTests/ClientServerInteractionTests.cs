using System;
using Xunit;
using DtronixJsonRpc;
using Xunit.Abstractions;
using System.Threading.Tasks;
using System.Threading;

namespace DtronixJsonRpcTests {
	public class ClientServerInteractionTests {

		private const int RESET_TIMEOUT = 5000;

		private readonly ITestOutputHelper output;

		private JsonRpcServer<TestActionHandler> Server { get; }
		private JsonRpcConnector<TestActionHandler> Client { get; }

		public ClientServerInteractionTests(ITestOutputHelper output) {
			this.output = output;
			Server = new JsonRpcServer<TestActionHandler>();
			Client = new JsonRpcConnector<TestActionHandler>("localhost");
			Client.Info.Username = "DefaultTestClient";
		}

		[Fact]
		public void ServerStartsAndStops() {
			var server_stated_reset = new ManualResetEvent(false);
			var server_stopped_reset = new ManualResetEvent(false);

			var waits = new WaitHandle[] { server_stated_reset, server_stopped_reset };

			Server.OnStart += (sender, e) => {
				server_stated_reset.Set();
			};

			Server.OnStop += (sender, e) => {
				server_stopped_reset.Set();
			};

			Server.Start();
			Server.Stop("Test completed");

			foreach (var wait in waits) {
				Assert.True(wait.WaitOne(RESET_TIMEOUT));
			}
		}

		[Fact]
		public void ClientConnectsAndDisconnectsStops() {
			var stated_reset = new ManualResetEvent(false);
			var stopped_reset = new ManualResetEvent(false);

			var waits = new WaitHandle[] { stated_reset, stopped_reset };



			Client.OnConnect += (sender, e) => {
				stated_reset.Set();
				Client.Disconnect("Test disconnection", JsonRpcSource.Client);
			};

			Client.OnDisconnect += (sender, e) => {
				stopped_reset.Set();
			};

			Server.Start();
			Client.Connect();

			foreach (var wait in waits) {
				Assert.True(wait.WaitOne(RESET_TIMEOUT));
			}

			Server.Stop("Test completed");
		}

		[Fact]
		public void ClientConnectsAndAuthenticates() {
			var called_method_reset = new ManualResetEvent(false);
			var waits = new WaitHandle[] { called_method_reset };


			Client.Actions.TestClientActions.MethodCalled += (sender, e) => {
				if (e.Type == typeof(TestClientActions<TestActionHandler>)) {
					if (e.Method == "Test") {
						called_method_reset.Set();
					}
				}

			};

			Client.OnConnect += (sender, e) => {
				Server.Clients[0].Actions.TestClientActions.Test(new TestClientActions<TestActionHandler>.TestClientActionTestArgs());
			};

			Client.OnAuthorizationRequest += (sender, e) => {
				e.Data = "AUTHENTICATION_DATA";
			};

			Server.OnAuthenticationVerification += (sender, e) => {
				e.Authenticated = e.Data == "AUTHENTICATION_DATA";
			};

			Server.Start();
			Client.Connect();

			foreach (var wait in waits) {
				Assert.True(wait.WaitOne(RESET_TIMEOUT));
			}

			Server.Stop("Test completed");
		}

		[Fact]
		public void ClientConnectsAndFailsAuthentication() {
			var auth_failure_reset = new ManualResetEvent(false);
			var client_disconnected_reset = new ManualResetEvent(false);

			var waits = new WaitHandle[] { auth_failure_reset, client_disconnected_reset };

			Client.OnAuthorizationRequest += (sender, e) => {
				e.Data = "FALSE_AUTHENTICATION_DATA";
			};

			Client.OnAuthorizationFailure += (sender, e) => {
				auth_failure_reset.Set();
            };

			Client.OnDisconnect += (sender, e) => {
				client_disconnected_reset.Set();
            };

			Server.OnAuthenticationVerification += (sender, e) => {
				e.Authenticated = false;
			};

			Server.Start();
			Client.Connect();

			foreach (var wait in waits) {
				Assert.True(wait.WaitOne(RESET_TIMEOUT));
			}

			Server.Stop("Test completed");
		}

		[Fact]
		public void ServerStartsAndClientConnects() {
			var called_method_reset = new ManualResetEvent(false);
			Server.Start();

			Client.OnConnect += (sender, e) => {
				called_method_reset.Set();
			};
			
			Client.Connect();

			Assert.True(called_method_reset.WaitOne(RESET_TIMEOUT));
			Server.Stop("Test completed");
		}
		

		[Fact]
		public void ServerCallesClientMethod() {
			var called_method_reset = new ManualResetEvent(false);
			Server.Start();
			var random_long = (long)(new Random().NextDouble());

			Client.OnConnect += (sender, e) => {
				Server.Clients[0].Actions.TestClientActions.Test(new TestClientActions<TestActionHandler>.TestClientActionTestArgs() {
					RandomLong = random_long
				});
			};

			Client.Actions.TestClientActions.MethodCalled += (sender, e) => {
				if(e.Type == typeof(TestClientActions<TestActionHandler>)) {
					if (e.Method == "Test") {
						Assert.Equal(random_long, ((TestClientActions<TestActionHandler>.TestClientActionTestArgs)e.Arguments).RandomLong);
						called_method_reset.Set();
					}
				}
			
			};


			Client.Connect();
			Assert.True(called_method_reset.WaitOne(RESET_TIMEOUT), "Test did not complete in a timely mannor.");
			Server.Stop("Test completed");
		}
	}
}
