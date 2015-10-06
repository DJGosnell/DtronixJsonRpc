using DtronixJsonRpc;
using NLog;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace DtronixJsonRpcTests {
	class TestBenchmarkActions<THandler> : JsonRpcActions<THandler> 
		where THandler : ActionHandler<THandler>, new(){

        private static Logger logger = LogManager.GetCurrentClassLogger();
        private Stopwatch stop_watch = null;
        private Stopwatch overall_stop_watch = null;
        private int call_times = 0;

        public TestBenchmarkActions(JsonRpcConnector<THandler> connector) : base(connector) {  }

        public class TimeBetweenCallsArgs : JsonRpcActionArgs {
            public bool CloseClient { get; set; }
            public int MaxCalls { get; set; }
        }

		[ActionMethod(JsonRpcSource.Client)]
		public void TimeBetweenCalls(TimeBetweenCallsArgs args) {
            if (SendAndReceived(args, nameof(TestBenchmarkActions<THandler>))) {
                Interlocked.Increment(ref call_times);

                if (stop_watch == null) {
                    stop_watch = Stopwatch.StartNew();
                    overall_stop_watch = Stopwatch.StartNew();
                    //logger.Trace("Started timer for calls.");
                } else {

                    //logger.Trace("Time from last call to now: {0} ms. Call number {1}", stop_watch.ElapsedMilliseconds, call_times);
                    stop_watch.Restart();
                }

                if (call_times == args.MaxCalls) {
                    long time = overall_stop_watch.ElapsedMilliseconds;
                    logger.Trace("Total calls {0} completed in {1} ms. Estimated {2:0} calls per second", call_times, time, call_times / (time / 1000d));
                    Connector.Disconnect("Test completed", JsonRpcSource.Client);
                }
            }

		}

	}
}
