using System;

namespace DtronixJsonRpc {
	public class AuthenticationFailureEventArgs : EventArgs {
		public string Reason { get; set; }


		public AuthenticationFailureEventArgs(string reason) {
			Reason = reason;
		}
	}
}
