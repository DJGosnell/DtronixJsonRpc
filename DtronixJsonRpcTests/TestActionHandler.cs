using DtronixJsonRpc;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DtronixJsonRpcTests {
	class TestActionHandler : ActionHandler {

		//private Dictionary<string, JsonRpcActions<TestActionHandler>> actions = new Dictionary<string, JsonRpcActions<TestActionHandler>>();

		public ClientActions<TestActionHandler> _ClientActions = null;
		public ClientActions<TestActionHandler> ClientActions {
			get {
				return _ClientActions ?? (_ClientActions = new ClientActions<TestActionHandler>());
			}
		}

		public TestActionHandler(JsonRpcConnector<TestActionHandler> connector) {
			Connector = connector;
        }

	}
}
