using System;
using System.Net.Sockets;

namespace DtronixJsonRpc {
	public class ClientDisconnectEventArgs<THandler> : EventArgs 
		where THandler : ActionHandler<THandler>, new() {

		public string Reason { get; set; }
		public SocketError Error { get; set; } = SocketError.Success;
		public JsonRpcSource DisconnectSource { get; set; }

		public JsonRpcServer<THandler> Server { get; set; }
		public JsonRpcClient<THandler> Client { get; set; }

		public ClientDisconnectEventArgs(string reason, JsonRpcSource disconnect_source, JsonRpcServer<THandler> server, JsonRpcClient<THandler> client, SocketError socket_error = SocketError.Success) {
			Server = server;
			Client = client;
			DisconnectSource = disconnect_source;
			Reason = reason;
			Error = socket_error;

		}
	}
}
