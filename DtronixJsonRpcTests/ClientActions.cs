using DtronixJsonRpc;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DtronixJsonRpcTests {
	class ClientActions<THandler> : JsonRpcActions<THandler>
		where THandler : IActionHandler{

		public ClientActions(JsonRpcConnector<THandler> connector) : base(connector) {  }

		public void Test(JsonRpcActionArgs args) {
			if (SendAndReceived(args, nameof(ClientActions<THandler>))) {
				Debug.WriteLine("Received dummy information from the server!");
			}

		}
	}
}
