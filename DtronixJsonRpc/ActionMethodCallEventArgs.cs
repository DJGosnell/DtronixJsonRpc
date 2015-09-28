using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace DtronixJsonRpc {
	public class ActionMethodCallEventArgs : EventArgs {
		public string Method { get; set; }
		public object Data { get; set; }

		public ActionMethodCallEventArgs(string method, object data) {
			Method = method;
			Data = data;

		}
	}
}
