using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DtronixJsonRpc {
	public class ConnectedClientChangedEventArgs : EventArgs {
		public ClientInfo[] Clients { get; set; }

		public ConnectedClientChangedEventArgs(ClientInfo[] clients) {
			Clients = clients;

		}
	}
}
