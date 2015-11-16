using DtronixJsonRpc;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DtronixJsonRpcTests.Actions {
	public class TestActionHandler : ActionHandler<TestActionHandler> {

		private TestClientActions _TestClientActions = null;
		public TestClientActions TestClientActions {
			get {
				return _TestClientActions ?? (_TestClientActions = new TestClientActions(Connector));
			}
		}

		private TestServerActions _TestServerActions = null;
		public TestServerActions TestServerActions {
			get {
				return _TestServerActions ?? (_TestServerActions = new TestServerActions(Connector));
			}
		}
	}
}
