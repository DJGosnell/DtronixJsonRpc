using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DtronixJsonRpc{
    public class ClientInfo : JsonRpcActionArgs {
        public int Id { get; set; } = -1;
        public string Username { get; set; }
        public ClientStatus Status { get; set; } = ClientStatus.Disconnected;
		public string DisconnectReason { get; set; }
	}
}
