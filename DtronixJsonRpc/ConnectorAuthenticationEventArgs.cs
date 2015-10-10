using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace DtronixJsonRpc {
	public class ConnectorAuthenticationEventArgs : EventArgs {
		private string _Data;

		/// <summary>
		/// Data for the server to verify.
		/// </summary>
		public string Data {
			get { return _Data; }
			set {
				if (value.Length > 2048) {
					throw new InvalidOperationException("Authentication request argument is too long to pass to server. Keep the length under 2048 characters");
				}
				_Data = value;
			}
		}

		/// <summary>
		/// Set to true if the user has passed valid connection credentials.
		/// </summary>
		public bool Authenticated { get; set; } = false;

		public ConnectorAuthenticationEventArgs() {
		}
	}
}
