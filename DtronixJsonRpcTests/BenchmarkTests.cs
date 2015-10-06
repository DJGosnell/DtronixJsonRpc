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
	public class BenchmarkTests : IDisposable {

        private static Logger logger = LogManager.GetCurrentClassLogger();

        private const int RESET_TIMEOUT = 5000;

		private readonly ITestOutputHelper output;

		private JsonRpcServer<TestActionHandler> Server { get; }
		private JsonRpcConnector<TestActionHandler> Client { get; }

		private List<Tuple<string, WaitHandle>> waits = new List<Tuple<string, WaitHandle>>();

		public BenchmarkTests(ITestOutputHelper output) {
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
		public void ClientCallMethodBenchmark() {
            Stopwatch sw;
            int itterations = 1000;
            Server.OnClientConnect += (sender, e) => {
                sw = Stopwatch.StartNew();
                for (int i = 0; i < itterations; i++) {
                    e.Client.Actions.TestBenchmarkActions.TimeBetweenCalls((new TestBenchmarkActions<TestActionHandler>.TimeBetweenCallsArgs() { MaxCalls = itterations }));
                }
            };

            Server.OnClientDisconnect += (sender, e) => {
                sender.Stop("Test completed");
            };


			StartServerConnectClient();

		}

        [Fact]
        public void ClientConcurrentCallMethodBenchmark() {
            Stopwatch sw;
            int itterations = 1000;
            Server.OnClientConnect += (sender, e) => {
                sw = Stopwatch.StartNew();
                Parallel.For(0, itterations, (i) => {
                    e.Client.Actions.TestBenchmarkActions.TimeBetweenCalls((new TestBenchmarkActions<TestActionHandler>.TimeBetweenCallsArgs() { MaxCalls = itterations }));
                });
            };

            Server.OnClientDisconnect += (sender, e) => {
                sender.Stop("Test completed");
            };


            StartServerConnectClient();

        }




        /// <summary>
        /// Helper to start the server and start the client once the server has loaded.
        /// </summary>
        private void StartServerConnectClient() {
			Server.OnStart += (sender, e) => {
				Task.Run(() => Client.Connect());
				
			};


			Server.Start();
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
