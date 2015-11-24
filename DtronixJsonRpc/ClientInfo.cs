using System;

namespace DtronixJsonRpc {
	public class ClientInfo {

		private int _Id;

		public int Id {
			get { return _Id; }
			set {
				_Id = value;
			}
		}


		private string _Username;

		/// <summary>
		/// Username that this client will be known by on the server.
		/// </summary>
		public string Username {
			get { return _Username; }
			set {
				if (value?.Length > 64) {
					throw new InvalidOperationException("Username must be under 64 characters.");
				}

				_Username = value;
			}
		}

		private ClientStatus _Status;

		public ClientStatus Status {
			get { return _Status; }
			set {
				_Status = value;
			}
		}


		private string _DisconnectReason;

		/// <summary>
		/// Reason that the client has disconnect.  Null if the client is not disconnecting.
		/// </summary>
		public string DisconnectReason {
			get { return _DisconnectReason; }
			set {
				if (value?.Length > 1024) {
					throw new InvalidOperationException("Reason for disconnection must be under 1024 characters.");
				}

				_DisconnectReason = value;
			}
		}


		private string _Version;

		/// <summary>
		/// Version of the client.
		/// </summary>
		public string Version {
			get { return _Version; }
			set {
				if (value?.Length > 16) {
					throw new InvalidOperationException("Version must be under 16 characters.");
				}

				_Version = value;
			}
		}


	}
}
