using DtronixJsonRpc;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DtronixJsonRpcTests {
	class TestClientActions : JsonRpcActions<TestActionHandler> {

		public event EventHandler<TestClientActions, TestClientMethodCalledEventArgs> MethodCalled;

		public JsonRpcConnector<TestActionHandler> Connector { get { return this.connector; } }

		public TestClientActions(JsonRpcConnector<TestActionHandler> connector) : base(connector) {  }


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
