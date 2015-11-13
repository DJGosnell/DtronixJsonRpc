using System;

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
