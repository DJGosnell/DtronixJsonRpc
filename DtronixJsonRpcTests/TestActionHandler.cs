using DtronixJsonRpc;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DtronixJsonRpcTests {
	class TestActionHandler : ActionHandler<TestActionHandler> {

		private TestClientActions<TestActionHandler> _TestClientActions = null;
		public TestClientActions<TestActionHandler> TestClientActions {
			get {
				return _TestClientActions ?? (_TestClientActions = new TestClientActions<TestActionHandler>(Connector));
			}
		}

	}
}
