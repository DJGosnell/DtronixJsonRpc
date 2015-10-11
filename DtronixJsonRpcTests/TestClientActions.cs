using DtronixJsonRpc;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DtronixJsonRpcTests {
	class TestClientActions<THandler> : JsonRpcActions<THandler> 
		where THandler : ActionHandler<THandler>, new(){

		public event EventHandler<TestClientActions<THandler>, TestClientMethodCalledEventArgs> MethodCalled;

		public TestClientActions(JsonRpcConnector<THandler> connector) : base(connector) {  }


		public class TestClientActionTestArgs : JsonRpcActionArgs {
			public long RandomLong { get; set; }
		}

		[ActionMethod(JsonRpcSource.Client)]
		public void Test(TestClientActionTestArgs args) {
			if (SendAndReceived(args)) {
				MethodCalled?.Invoke(this, new TestClientMethodCalledEventArgs(args, GetType()));
			}

		}

		[ActionMethod(JsonRpcSource.Client)]
		public void Test2(TestClientActionTestArgs args) {
			if (SendAndReceived(args)) {
				MethodCalled?.Invoke(this, new TestClientMethodCalledEventArgs(args, GetType()));
			}

		}
	}
}
