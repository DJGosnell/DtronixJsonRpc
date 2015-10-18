using DtronixJsonRpc;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace DtronixJsonRpcTests {
	public class TestClientActions : JsonRpcActions<TestActionHandler> {

		public event EventHandler<TestClientActions, TestClientMethodCalledEventArgs> MethodCalled;

		new public JsonRpcConnector<TestActionHandler> Connector { get { return base.Connector; } }

		public TestClientActions(JsonRpcConnector<TestActionHandler> connector, [CallerMemberName] string member_name = "") : base(connector, member_name) {

		}


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
