using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DtronixJsonRpc {
	public class AuthenticationFailureEventArgs : EventArgs {
		public string Reason { get; set; }


		public AuthenticationFailureEventArgs(string reason) {
			Reason = reason;
		}
	}
}
