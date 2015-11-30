using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DtronixJsonRpc {
	internal class ClientConnectionResponse {

		/// <summary>
		/// Name of the server.
		/// </summary>
		public string ServerName { get; set; }

		/// <summary>
		/// Set to a non-null value to let the other end of the connection know there is an issue with the connection.
		/// </summary>
		public string Error { get; set; }

		/// <summary>
		/// ID number of the client.
		/// </summary>
		public int ClientId { get; set; }

		/// <summary>
		/// Abstract data from the server.
		/// </summary>
		public JToken ServerData { get; set; }

		/// <summary>
		/// Version of the other end of the connection
		/// </summary>
		public string Version { get; internal set; }

		/// <summary>
		/// Set to true if the client was connecting as an anonymous client.
		/// The server disconnects the client immediately after sending the server information.
		/// </summary>
		public bool AnonymousClient { get; set; }

		public ClientConnectionResponse() {

		}
		public ClientConnectionResponse(string error) {
			Error = error;
		}
	}
}
