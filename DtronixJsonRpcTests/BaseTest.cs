using DtronixJsonRpc;
using NLog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using Xunit.Abstractions;

namespace DtronixJsonRpcTests {
    public class BaseTest : IDisposable {


		private static Logger logger = LogManager.GetCurrentClassLogger();

		protected const int RESET_TIMEOUT = 5000;

        protected readonly ITestOutputHelper output;

        protected JsonRpcServer<TestActionHandler> Server { get; }
        protected JsonRpcClient<TestActionHandler> Client { get; }

		protected int port;

		private static int port_increment = 2828;

        protected List<Tuple<string, WaitHandle>> waits = new List<Tuple<string, WaitHandle>>();

        public BaseTest(ITestOutputHelper output) {
			port = Interlocked.Increment(ref port_increment);

            var server_stop_reset = AddWait("Server stop");

            this.output = output;
            Server = new JsonRpcServer<TestActionHandler>(port);
            Client = new JsonRpcClient<TestActionHandler>("localhost", port);
            Client.Info.Username = "DefaultTestClient";

			Server.OnStop += (sender, e) => {
                server_stop_reset.Set();
            };
        }


        /// <summary>
        /// Helper to start the server and start the client once the server has loaded.
        /// </summary>
        protected void StartServerConnectClient() {
            Server.OnStart += (sender, e) => {
                Task.Run(() => Client.Connect());

            };


            Server.Start();
        }


        protected ManualResetEvent AddWait(string description) {
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
