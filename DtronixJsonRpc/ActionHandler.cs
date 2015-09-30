using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DtronixJsonRpc {
	public class ActionHandler<T> : IActionHandler {

		public JsonRpcConnector<IActionHandler> Connector { get; set; }

		protected bool SendAndReceived(object args, [System.Runtime.CompilerServices.CallerMemberName] string member_name = "") {
			if (Connector.Mode == JsonRpcSource.Server) {
				Connector.Send(member_name, args);
				return false;
			} else {
				return true;
			}
		}

		public void ExecuteAction(string method, object obj) {
			var method_info = GetType().GetMethod(method);

			if (method_info == null) {
				throw new InvalidOperationException("Method requested by the server does not exist.");
			}

			if (method_info.GetCustomAttributes(typeof(ActionMethodAttribute), true)?.Length == 0) {
				throw new InvalidOperationException("Method requested by the server is not allowed to be called.");
			}

			object[] args = null;

			var param_count = method_info.GetParameters().Length;

			if (param_count == 0) {
				args = null;

			} else if (param_count == 1) {
				args = new object[] { obj };

			} else {
				throw new ArgumentException("Method called does not match any signature.");
			}


			try {
				method_info.Invoke(this, args);

			} catch (Exception e) {
				throw e;
			}

		}
	}
}
