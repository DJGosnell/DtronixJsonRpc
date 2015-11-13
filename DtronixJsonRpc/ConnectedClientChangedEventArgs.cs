using System;

namespace DtronixJsonRpc {
	public class ConnectedClientChangedEventArgs : EventArgs {
		public ClientInfo[] Clients { get; set; }

		public ConnectedClientChangedEventArgs(ClientInfo[] clients) {
			Clients = clients;

		}
	}
}
