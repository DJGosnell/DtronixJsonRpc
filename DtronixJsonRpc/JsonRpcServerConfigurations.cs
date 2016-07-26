﻿using Newtonsoft.Json.Linq;
using System.Net;

namespace DtronixJsonRpc {
	/// <summary>
	/// Configurations for the server and subsequent clients.
	/// </summary>
	public class JsonRpcServerConfigurations {

		public enum TransportMode {
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
		/// Sets the data transport of the server & connectors.
		/// 
		/// Default: BSON.
		/// </summary>
		public TransportMode TransportProtocol { get; set; } = TransportMode.Bson;

		/// <summary>
		/// Set to true to automatically ping all clients when connected to ensure they are still connected.
		/// 
		/// Default: true.
		/// </summary>
		public bool PingClients { get; set; } = true;

		/// <summary>
		/// Number of milliseconds between client pings.
		/// 
		/// Default: 5 seconds.
		/// </summary>
		public int PingFrequency { get; set; } = 5 * 1000;

		/// <summary>
		/// If the ping time exceeds the specified number of milliseconds, the client will be automatically disconnected.
		/// 
		/// Default: 15 seconds.
		/// </summary>
		public int PingTimeoutDisconnectTime { get; set; } = 30 * 1000;
	}


}
