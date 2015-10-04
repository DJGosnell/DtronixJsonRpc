using System;
using Xunit;
using DtronixJsonRpc;
using Xunit.Abstractions;
using System.Threading.Tasks;
using System.Threading;
using System.Collections.Generic;

namespace DtronixJsonRpcTests {
	public class ClientServerInteractionTests : IDisposable {

		private const int RESET_TIMEOUT = 5000;

		private readonly ITestOutputHelper output;

		private JsonRpcServer<TestActionHandler> Server { get; }
		private JsonRpcConnector<TestActionHandler> Client { get; }

		private List<Tuple<string, WaitHandle>> waits = new List<Tuple<string, WaitHandle>>();

		public ClientServerInteractionTests(ITestOutputHelper output) {
			var server_stop_reset = AddWait("Server stop");

			this.output = output;
			Server = new JsonRpcServer<TestActionHandler>();
			Client = new JsonRpcConnector<TestActionHandler>("localhost");
			Client.Info.Username = "DefaultTestClient";

			Server.OnStop += (sender, e) => {
				server_stop_reset.Set();
			};
		}

		[Fact]
		public void ServerStartsAndStops() {
			var server_start_reset = AddWait("Server start");
			var server_stopped_reset = AddWait("Server stop");

			Server.OnStart += (sender, e) => {
				server_start_reset.Set();
			};

			Server.OnStop += (sender, e) => {
				server_stopped_reset.Set();
			};


			Server.Start();
			Server.Stop("Test completed");

		}

		[Fact]
		public void ClientConnectsAndDisconnectsStops() {
			var started_reset = AddWait("Client start");
			var stopped_reset = AddWait("Client stop");

			var waits = new WaitHandle[] { started_reset, stopped_reset };



			Client.OnConnect += (sender, e) => {
				started_reset.Set();
				Client.Disconnect("Test disconnection", JsonRpcSource.Client);
			};

			Client.OnDisconnect += (sender, e) => {
				stopped_reset.Set();
			};

			Server.OnClientDisconnect += (sender, e) => {
				Server.Stop("Test completed");
			};

			Server.Start();
			Client.Connect();				
		}

		[Fact]
		public void ClientConnectsAndAuthenticates() {
			var called_method_reset = AddWait("Method call");

			Client.Actions.TestClientActions.MethodCalled += (sender, e) => {
				if (e.Type == typeof(TestClientActions<TestActionHandler>)) {
					if (e.Method == "Test") {
						called_method_reset.Set();
						Server.Stop("Test completed");
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
		}

		[Fact]
		public void ClientConnectsAndFailsAuthentication() {
			var auth_failure_reset = AddWait("Authorization fail");
			var client_disconnected_reset = AddWait("Client disconnect");

			Client.OnAuthorizationRequest += (sender, e) => {
				e.Data = "FALSE_AUTHENTICATION_DATA";
			};

			Client.OnAuthorizationFailure += (sender, e) => {
				auth_failure_reset.Set();
            };

			Client.OnDisconnect += (sender, e) => {
				client_disconnected_reset.Set();
				Server.Stop("Test completed");
			};

			Server.OnAuthenticationVerification += (sender, e) => {
				e.Authenticated = false;
			};


			Server.Start();
			Client.Connect();


		}

		[Fact]
		public void ServerStartsAndClientConnects() {
			var called_method_reset = AddWait("Client connect");

			Server.Start();

			Client.OnConnect += (sender, e) => {
				called_method_reset.Set();
				Server.Stop("Test completed");
			};


			Client.Connect();
		}
		

		[Fact]
		public void ServerCallsClientMethod() {
			var called_method_reset = AddWait("Method call");

			Server.Start();
			var random_long = 1684584139;

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
						Client.Disconnect("Test completed", JsonRpcSource.Client);
					}
				}
			
			};

			Server.OnClientDisconnect += (sender, e) => {
				Server.Stop("Test completed");
			};


			Client.Connect();
		}

		private ManualResetEvent AddWait(string description) {
			var wait = new ManualResetEvent(false);
			waits.Add(new Tuple<string, WaitHandle>(description, wait));
			return wait;
		}

		public void Dispose() {
			foreach (var wait in waits) {
				Assert.True(wait.Item2.WaitOne(RESET_TIMEOUT), "Did not activate reset event: " + wait.Item1);
			}
		}
	}
}
