using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace DtronixJsonRpc {
	/// <summary>
	/// Configurations for the server and subsequent clients.
	/// </summary>
	public class JsonRpcServerConfigurations {

		public enum JsonMode {
			/// <summary>
			/// Broadcast the data in UTF8 text JSON.
			/// </summary>
			Json,

			/// <summary>
			/// Broadcast the data in the BSON binary format.
			/// </summary>
			Bson
		}

		/// <summary>
		/// Sets the interface to bind the server instance on.
		/// Default: Listens on all interfaces.
		/// </summary>
		public IPAddress BindingAddress { get; set; } = IPAddress.Any;

		/// <summary>
		/// Sets the port to bind the server instance on. 
		/// Default: 2828.
		/// </summary>
		public int BindingPort { get; set; } = 2828;

		/// <summary>
		/// Sets whether or not to broadcast to the other clients a change in a client's status information. (Connected, disconnected)
		/// Default: true.
		/// </summary>
		public bool BroadcastClientStatusChanges { get; set; } = true;

		/// <summary>
		/// Sets the data mode of the server & connectors.
		/// 
		/// Default: BSON.
		/// </summary>
		public JsonMode DataMode { get; set; } = JsonMode.Bson;

		public bool PingClients { get; set; } = true;

		public int PingFrequency { get; set; } = 5000;

		public int PingTimeoutDisconnectTime { get; set; } = 15 * 1000;


	}
}
