using DtronixJsonRpc;
using DtronixJsonRpcTests.Actions;
using Newtonsoft.Json;
using Newtonsoft.Json.Bson;
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
	public class JsonRpcClientIntegrationTests {

		public static int port = 2827;

		private ManualResetEvent wait = new ManualResetEvent(false);

		private JsonRpcServer<TestActionHandler> server;
		private JsonRpcClient<TestActionHandler> client;
		private JsonSerializer serializer = new JsonSerializer();
		private Task server_task;
		private Task client_task;

		private const string AUTH_TEXT = "ArbitraryAuthText";
		private ITestOutputHelper output;

		public JsonRpcClientIntegrationTests(ITestOutputHelper output) {
			this.output = output;

			var configs = new JsonRpcServerConfigurations() {
				BindingPort = Interlocked.Increment(ref port),
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

		[Fact]
		public async void ReturnTrue_multiple_calls_succeed() {
			var iterations = 100;

			server_task = new Task(() => {
				server.Start();
			});

			client_task = new Task(() => {

				client.OnConnect += async (sender, e) => {
					// First call to clear the way.
					await client.Actions.TestServerActions.ReturnTrue(new TestServerActions.TestArgs() {
						RandomLong = 2395715
					});

					var sw = System.Diagnostics.Stopwatch.StartNew();
					for (int i = 0; i < iterations; i++) {
						var result = await client.Actions.TestServerActions.ReturnTrue(new TestServerActions.TestArgs() {
							RandomLong = 2395715
						});
						Assert.True(result);
					}

					sw.Stop();

					output.WriteLine($"{sw.ElapsedMilliseconds}ms to complete the iterations. {iterations / (sw.ElapsedMilliseconds / 1000d)} Calls per second.");

					CompleteTest();

				};

				client.Connect();
			});



			await StartAndWaitClient();

		}

		[Fact]
		public async void ReturnTrue_call_returns_true() {
			server_task = new Task(() => {
				server.Start();
			});

			client_task = new Task(() => {

				client.OnConnect += async (sender, e) => {
					var result = await client.Actions.TestServerActions.ReturnTrue(new TestServerActions.TestArgs() {
						RandomLong = 2395715
					});

					Assert.True(result);

					CompleteTest();

				};

				client.Connect();
			});



			await StartAndWaitClient();

		}

		[Fact]
		public async void ReturnFalse_call_returns_false() {
			server_task = new Task(() => {
				server.Start();
			});

			client_task = new Task(() => {

				client.OnConnect += async (sender, e) => {
					var result = await client.Actions.TestServerActions.ReturnFalse(new TestServerActions.TestArgs() {
						RandomLong = 2395715
					});

					Assert.False(result);

					CompleteTest();

				};

				client.Connect();
			});



			await StartAndWaitClient();

		}

		[Fact]
		public async void ReturnTrueWithoutParams_call_returns_true() {
			server_task = new Task(() => {
				server.Start();
			});

			client_task = new Task(() => {

				client.OnConnect += async (sender, e) => {
					var result = await client.Actions.TestServerActions.ReturnTrueWithoutParams();

					Assert.True(result);

					CompleteTest();

				};

				client.Connect();
			});



			await StartAndWaitClient();

		}

		[Fact]
		public async void Notify_call_sends_to_server() {
			server_task = new Task(() => {
				server.OnClientConnect += (sender, e) => {

					e.Client.OnDataReceived += (sender2, e2) => {
						if (e2.Data["method"].ToString() == "TestServerActions.NotifyServer") {
							CompleteTest();
						}
					};
				};

				server.Start();
			});

			client_task = new Task(() => {

				client.OnConnect += (sender, e) => {
					client.Actions.TestServerActions.NotifyServer(new TestServerActions.TestArgs() {
						RandomLong = 2395715
					});

				};

				client.Connect();
			});



			await StartAndWaitClient();

		}

		public async Task<bool> StartAndWaitClient(int wait_duration = 5000) {
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


		[Fact]
		public async void Ping_client_times_out() {
			server.Configurations.PingFrequency = 250;
			server.Configurations.PingTimeoutDisconnectTime = 500;

			server = new JsonRpcServer<TestActionHandler>(server.Configurations);

			server_task = new Task(() => {
				server.OnClientConnect += (sender, e) => {
					e.Client.Actions.TestClientActions.BlockThread(2000);
				};

				server.OnClientDisconnect += (sender, e) => {
					CompleteTest();
				};

				server.Start();
			});

			client_task = new Task(() => {
				client.Connect();
			});

			await StartAndWaitClient();
		}

		[Fact]
		public async void Ping_does_not_times_out() {
			server.Configurations.PingFrequency = 250;
			server.Configurations.PingTimeoutDisconnectTime = 500;

			server = new JsonRpcServer<TestActionHandler>(server.Configurations);

			server_task = new Task(() => {
				server.OnClientConnect += (sender, e) => {


					Task.Run(async () => {
						await Task.Delay(1000);

						if (e.Client.Info.Status == ClientStatus.Connected) {
							CompleteTest();
						} else {
							FailTest();
						 }
					});
				};

				server.Start();
			});

			client_task = new Task(() => {
				client.Connect();
			});

			await StartAndWaitClient();
		}

		private void CompleteTest() {
			server.Stop("Test completed successfully.");
		}

		private void FailTest() {
			server.Stop("Test failed.");
		}

	}
}
