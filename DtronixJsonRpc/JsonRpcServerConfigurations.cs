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
		public int PingFrequency { get; set; } = 5000;

		/// <summary>
		/// If the ping time exceeds the specified number of milliseconds, the client will be automatically disconnected.
		/// 
		/// Default: 15 seconds.
		/// </summary>
		public int PingTimeoutDisconnectTime { get; set; } = 15 * 1000;

		/// <summary>
		/// If set to true, when the client attempts to connect, it will be required to have the same version number as the server.
		/// 
		/// Default: true;
		/// </summary>
		public bool RequireSameVersion { get; set; } = true;


		/// <summary>
		/// If set to false, when the client attempts to connect, it's username will be checked along the other connected clients.
		/// If there is a duplicate username, the new client will be disconnected.
		/// 
		/// Default: false;
		/// </summary>
		public bool AllowDuplicateUsernames { get; set; } = false;


		/// <summary>
		/// If set to true, a client will be allowed to connect and get server information then disconnect.
		/// An anonymous user can not send any information to the server.
		/// 
		/// Default: false.
		/// </summary>
		public bool AllowAnonymousConnections { get; set; } = false;


		/// <summary>
		/// Gets or sets the amount of time a client will wait for a send operation to complete successfully.
		/// </summary>
		public int ClientSendTimeout { get; set; } = 10000;
	}


}
