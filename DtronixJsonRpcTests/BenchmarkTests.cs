using System;
using Xunit;
using DtronixJsonRpc;
using Xunit.Abstractions;
using System.Threading.Tasks;
using System.Threading;
using System.Collections.Generic;
using NLog;
using System.Diagnostics;

namespace DtronixJsonRpcTests {
	public class BenchmarkTests : BaseTest {

		public BenchmarkTests(ITestOutputHelper output) : base(output) {
		}

		[Fact]
		public void ClientCallMethodBenchmark() {
			Stopwatch sw;
			int itterations = 1000;
			Server.OnClientConnect += (sender, e) => {
				sw = Stopwatch.StartNew();
				for (int i = 0; i < itterations; i++) {
					e.Client.Actions.TestBenchmarkActions.TimeBetweenCalls(new TestBenchmarkActions.TimeBetweenCallsArgs() {
						MaxCalls = itterations
					});
				}
			};

			Server.OnClientDisconnect += (sender, e) => {
				sender.Stop("Test completed");
			};


			StartServerConnectClient();

		}

		[Fact]
		public void ClientConcurrentCallMethodBenchmark() {
			int itterations = 1000;

			Server.OnClientConnect += (sender, e) => {
				Parallel.For(0, itterations, (i) => {
					e.Client.Actions.TestBenchmarkActions.TimeBetweenCalls(new TestBenchmarkActions.TimeBetweenCallsArgs() { MaxCalls = itterations });
				});
			};

			Server.OnClientDisconnect += (sender, e) => {
				sender.Stop("Test completed");
			};


			StartServerConnectClient();

		}

		[Fact]
		public void ServerCallMethodBenchmark() {

			int itterations = 100;
			int clients = 4;
			var client_list = new List<JsonRpcClient<TestActionHandler>>();

			Server.OnStart += (sender, e) => {
				for (int i = 0; i < clients; i++) {
					Task.Factory.StartNew((object number) => {
						var client = JsonRpcClient<TestActionHandler>.CreateClient("localhost", port);
						client_list.Add(client);
						client.Info.Username = "DefaultTestClient" + number;

						client.OnConnect += (sender2, e2) => {
							for (int j = 0; j < itterations; j++) {

								sender2.Actions.TestBenchmarkActions.TimeBetweenCalls(new TestBenchmarkActions.TimeBetweenCallsArgs() { MaxCalls = itterations });
							}
						};

						client.Connect();
					}, i);
				}
			};

			Server.OnClientDisconnect += (sender, e) => {
				if (TestBenchmarkActions.call_times == (itterations * clients)) {
					sender.Stop("Test completed");
				}
			};


			Server.Start();

			Server.OnStart += (sender, e) => {
				Task.Run(() => Client.Connect());

			};



		}

	}
}
