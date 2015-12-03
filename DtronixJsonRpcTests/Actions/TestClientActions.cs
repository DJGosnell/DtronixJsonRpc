using DtronixJsonRpc;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace DtronixJsonRpcTests.Actions {
	public class TestClientActions : JsonRpcActions<TestActionHandler> {

		new public JsonRpcClient<TestActionHandler> Connector { get { return base.Connector; } }

		public TestClientActions(JsonRpcClient<TestActionHandler> connector, [CallerMemberName] string member_name = "") : base(connector, member_name) {

		}


		public class TestArgs {
			public long RandomLong { get; set; }
		}

		[ActionMethod(JsonRpcSource.Client)]
		public void BlockThread(int pause_time, string id = null) {
			if (Notify(pause_time, ref id)) {
				Thread.Sleep(pause_time);
			}
		}


		/*[ActionMethod(JsonRpcSource.Client)]
		public void Test(TestArgs args, bool received = false) {
			if (ReturnRemote(args, received)) {
				//((BaseTest)Connector.DataObject).
				//MethodCalled?.Invoke(this, new TestClientMethodCalledEventArgs<object>(args, GetType()));
			}

		}

		[ActionMethod(JsonRpcSource.Client)]
		public void Noop(TestArgs args, bool received = false) {
			if (ReturnRemote(args, received)) {
			}
		}



		[ActionMethod(JsonRpcSource.Client)]
		public void Test2(TestArgs args, bool received = false) {
			if (ReturnRemote(args, received)) {
				//MethodCalled?.Invoke(this, new TestClientMethodCalledEventArgs<object>(args, GetType()));
			}

		}*/
	}
}
