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
	public class JsonRpcClientIntegrationTests : JsonRpcIntegrationTestBase {

		

		public JsonRpcClientIntegrationTests(ITestOutputHelper output) : base(output) {	}

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

	

	}
}
