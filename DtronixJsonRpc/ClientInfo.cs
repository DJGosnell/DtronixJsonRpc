using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DtronixJsonRpc{
    public class ClientInfo : JsonRpcActionArgs {
        public int Id { get; set; } = -1;

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

        public ClientStatus Status { get; set; } = ClientStatus.Disconnected;

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
	}
}
