using System;

namespace DtronixJsonRpc {
	public class ClientConnectEventArgs<THandler> : EventArgs 
		where THandler : ActionHandler<THandler>, new() {

		public JsonRpcServer<THandler> Server { get; set; }
		public JsonRpcClient<THandler> Client { get; set; }

		public ClientConnectEventArgs(JsonRpcServer<THandler> server, JsonRpcClient<THandler> client) {
			Server = server;
			Client = client;

		}
	}
}
