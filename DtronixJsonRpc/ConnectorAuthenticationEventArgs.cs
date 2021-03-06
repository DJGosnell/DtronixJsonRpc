﻿using System;

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
		/// String describing the reason the authentication failed.  Ignored except server.
		/// </summary>
		public string FailureReason { get; set; }

		/// <summary>
		/// Set to true if the user has passed valid connection credentials.
		/// </summary>
		public bool Authenticated { get; set; } = false;

		public ConnectorAuthenticationEventArgs() {
		}
	}
}
