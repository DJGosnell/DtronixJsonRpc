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

		public event EventHandler<TestClientActions, TestClientMethodCalledEventArgs<object>> MethodCalled;

		new public JsonRpcConnector<TestActionHandler> Connector { get { return base.Connector; } }

		public TestClientActions(JsonRpcConnector<TestActionHandler> connector, [CallerMemberName] string member_name = "") : base(connector, member_name) {

		}


		public class TestArgs {
			public long RandomLong { get; set; }
		}

		[ActionMethod(JsonRpcSource.Client)]
		public void Test(JsonRpcParam<TestArgs> param) {
			if (SendAndReceived(param)) {
				MethodCalled?.Invoke(this, new TestClientMethodCalledEventArgs<object>(param, GetType()));
			}

		}

		[ActionMethod(JsonRpcSource.Client)]
		public void Test2(JsonRpcParam<TestArgs> param) {
			if (SendAndReceived(param)) {
				MethodCalled?.Invoke(this, new TestClientMethodCalledEventArgs<object>(param, GetType()));
			}

		}
	}
}
