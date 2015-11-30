using Newtonsoft.Json.Linq;
using System;

namespace DtronixJsonRpc {
	public class ReceiveConnectionInformationEventArgs : EventArgs {

		/// <summary>
		/// Version information about the server.
		/// </summary>
		public string Version { get; set; }
		/// <summary>
		/// Name of the server.
		/// </summary>
		public string ServerName { get; set; } = "dtx-jsonrpc";

		/// <summary>
		/// Abstract data that about the server. (Description, Logo, etc...)
		/// </summary>
		public JToken ServerData { get; set; } = null;
	}
}