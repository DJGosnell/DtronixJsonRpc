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
			public int RandomInt { get; set; }
		}

		[ActionMethod]
		public void Test(TestClientActionTestArgs args) {
			if (SendAndReceived(args, nameof(TestClientActions<THandler>))) {

				MethodCalled?.Invoke(this, new TestClientMethodCalledEventArgs(args, GetType()));
                Debug.WriteLine("Received dummy information from the server!");
			}

		}
	}
}
