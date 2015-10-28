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

		new public JsonRpcClient<TestActionHandler> Connector { get { return base.Connector; } }

		public TestClientActions(JsonRpcClient<TestActionHandler> connector, [CallerMemberName] string member_name = "") : base(connector, member_name) {

		}


		public class TestArgs {
			public long RandomLong { get; set; }
		}

		[ActionMethod(JsonRpcSource.Client)]
		public void Test(TestArgs args, bool received = false) {
			if (SendAndReceived(args, received)) {
				Connector
				MethodCalled?.Invoke(this, new TestClientMethodCalledEventArgs<object>(args, GetType()));
			}

		}

		[ActionMethod(JsonRpcSource.Client)]
		public void Test2(TestArgs args, bool received = false) {
			if (SendAndReceived(args, received)) {
				MethodCalled?.Invoke(this, new TestClientMethodCalledEventArgs<object>(args, GetType()));
			}

		}
	}
}
