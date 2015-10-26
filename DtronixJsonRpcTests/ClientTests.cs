using System;
using Xunit;
using DtronixJsonRpc;
using Xunit.Abstractions;
using System.Threading.Tasks;
using System.Threading;
using System.Collections.Generic;

namespace DtronixJsonRpcTests {
	public class ClientTests : BaseTest {

		public ClientTests(ITestOutputHelper output) : base(output) {
		}


		[Fact]
		public void Disconnects() {
			var started_reset = AddWait("Client start");
			var stopped_reset = AddWait("Client stop");

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

			StartServerConnectClient();
		}

		[Fact]
		public void Authenticates() {
			var called_method_reset = AddWait("Method call");

			Client.Actions.TestClientActions.MethodCalled += (sender, e) => {
				if (e.Type == typeof(TestClientActions)) {
					if (e.Method == "Test") {
						called_method_reset.Set();
						Server.Stop("Test completed");
					}
				}

			};

			Client.OnConnect += (sender, e) => {
				Server.Clients[0].Actions.TestClientActions.Test(null);
			};

			Client.OnAuthenticationRequest += (sender, e) => {
				e.Data = "AUTHENTICATION_DATA";
			};

			Server.OnAuthenticationVerification += (sender, e) => {
				e.Authenticated = e.Data == "AUTHENTICATION_DATA";
			};


			StartServerConnectClient();
		}

		[Fact]
		public void FailsAuthentication() {
			var auth_failure_reset = AddWait("Authentication fail");
			var client_disconnected_reset = AddWait("Client disconnect");

			Client.OnAuthenticationRequest += (sender, e) => {
				e.Data = "FALSE_AUTHENTICATION_DATA";
			};

			Client.OnAuthenticationFailure += (sender, e) => {
				auth_failure_reset.Set();
            };

			Client.OnDisconnect += (sender, e) => {
				client_disconnected_reset.Set();
				Server.Stop("Test completed");
			};

			Server.OnAuthenticationVerification += (sender, e) => {
				e.Authenticated = false;
			};

			StartServerConnectClient();
		}


        [Fact]
        public void ClientConnects() {
            var called_method_reset = AddWait("Client connect");


            Client.OnConnect += (sender, e) => {
                called_method_reset.Set();
                Server.Stop("Test completed");
            };

            StartServerConnectClient();
        }





        [Fact]
        public void SimultaneousConnections() {


            List<JsonRpcConnector<TestActionHandler>> clients = new List<JsonRpcConnector<TestActionHandler>>();
            var random_long = 1684584139;
            var client_list = new List<JsonRpcConnector<TestActionHandler>>();
            var wait_list = new List<ManualResetEvent>();
            int client_count = 20;
            for (int i = 0; i < client_count; i++) {
                wait_list.Add(AddWait("Method call"));
            }

            Server.OnStart += (sender, e) => {

                for (int i = 0; i < client_count; i++) {
                    Task.Factory.StartNew((object state) => {
                        var client = new JsonRpcConnector<TestActionHandler>("localhost", port);
                        client_list.Add(client);
                        client.Info.Username = "DefaultTestClient" + (int)state;

                        client.Actions.TestClientActions.MethodCalled += (sender2, e2) => {
                            if (e2.Type == typeof(TestClientActions)) {
                                if (e2.Method == "Test") {
                                    Assert.Equal(random_long + sender2.Connector.Info.Id, ((TestClientActions.TestArgs)(e2.Arguments)).RandomLong);
                                    wait_list[(int)state].Set();
                                    client.Disconnect("Client test completed", JsonRpcSource.Client);
                                }
                            }

                        };

                        Task.Run(() => client.Connect());
                    }, i);
                }
            };

            Server.OnClientConnect += (sender2, e2) => {
				e2.Client.Actions.TestClientActions.Test(new TestClientActions.TestArgs() { 
					RandomLong = random_long + e2.Client.Info.Id
				});
            };

            Task.Run(() => Server.Start());


            foreach (var wait in waits) {
                if(wait.Item1 == "Server stop") {
                    continue;
                }

                Assert.True(wait.Item2.WaitOne(RESET_TIMEOUT), "Did not activate reset event: " + wait.Item1);
            }

            Server.Stop("Test completed");
        }
	}
}
