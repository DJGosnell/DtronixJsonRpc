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
		public async void TestReturnTrue_multiple_calls_succeed() {
			var iterations = 1000;

			server_task = new Task(() => {
				server.Start();
			});

			client_task = new Task(() => {

				client.OnConnect += async (sender, e) => {
					// First call to clear the way.
					await client.Actions.TestServerActions.TestReturnTrue(new TestServerActions.TestArgs() {
						RandomLong = 2395715
					});

					var sw = System.Diagnostics.Stopwatch.StartNew();
					for (int i = 0; i < iterations; i++) {
						var result = await client.Actions.TestServerActions.TestReturnTrue(new TestServerActions.TestArgs() {
							RandomLong = 2395715
						});
						Assert.True(result);
					}

					sw.Stop();

					output.WriteLine($"{sw.ElapsedMilliseconds} to complete the iterations. {iterations / (sw.ElapsedMilliseconds / 1000d)} Calls per second.");

					server.Stop("Test completed");

				};

				client.Connect();
			});



			await StartAndWaitClient();

		}

		[Fact]
		public async void TestReturnTrue_call_returns_true() {
			server_task = new Task(() => {
				server.Start();
			});

			client_task = new Task(() => {

				client.OnConnect += async (sender, e) => {
					var result = await client.Actions.TestServerActions.TestReturnTrue(new TestServerActions.TestArgs() {
						RandomLong = 2395715
					});

					Assert.True(result);

					server.Stop("Test completed");

				};

				client.Connect();
			});



			await StartAndWaitClient();

		}

		[Fact]
		public async void TestReturnTrue_call_returns_false() {
			server_task = new Task(() => {
				server.Start();
			});

			client_task = new Task(() => {

				client.OnConnect += async (sender, e) => {
					var result = await client.Actions.TestServerActions.TestReturnFalse(new TestServerActions.TestArgs() {
						RandomLong = 2395715
					});

					Assert.False(result);

					server.Stop("Test completed");

				};

				client.Connect();
			});



			await StartAndWaitClient();

		}

		public async Task<bool> StartAndWaitClient() {

			try {
				server_task.Start();
				client_task.Start();

				if (await Task.WhenAny(server_task, Task.Delay(5000)) != server_task) {
					client.Disconnect("Test failed to complete within the time limitation.");
					return false;
				}
			} finally {
				server_task.Exception?.Handle(ex => {
					throw ex;
				});

				client_task.Exception?.Handle(ex => {
					throw ex;
				});
			}

			return true;
		}

	}
}
