using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace DtronixJsonRpc {
    public class ClientDisconnectEventArgs : EventArgs {
        public string Reason { get; set; }
        public SocketError Error { get; set; } = SocketError.Success;
		public JsonRpcMode DisconnectSource { get; set; }

        public ClientDisconnectEventArgs(string reason, JsonRpcMode disconnect_source, SocketError socket_error = SocketError.Success) {
            DisconnectSource = disconnect_source;
            Reason = reason;
            Error = socket_error;

        }
    }
}
