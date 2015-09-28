using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace DtronixJsonRpc {
	public class ConnectorAuthroizationEventArgs : EventArgs {
		public string Data { get; set; }

		public ConnectorAuthroizationEventArgs(string data) {
			Data = data;

		}
	}
}
