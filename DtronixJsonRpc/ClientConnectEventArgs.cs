using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace DtronixJsonRpc {
	public class ClientConnectEventArgs<THandler> : EventArgs 
		where THandler : ActionHandler<THandler>, new() {

		public JsonRpcServer<THandler> Server { get; set; }
		public JsonRpcConnector<THandler> Client { get; set; }

		public ClientConnectEventArgs(JsonRpcServer<THandler> server, JsonRpcConnector<THandler> client) {
			Server = server;
			Client = client;

		}
	}
}
