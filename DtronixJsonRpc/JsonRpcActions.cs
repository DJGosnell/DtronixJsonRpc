using DtronixJsonRpc.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DtronixJsonRpc {
	public abstract class JsonRpcActions {

		protected readonly JsonRpcMode mode;
		protected readonly JsonRpcConnector<ServerActions, ClientActions> client_handler;
		protected readonly JsonRpcConnector<ClientActions, ServerActions> client;

		public JsonRpcActions(JsonRpcConnector client, JsonRpcConnector<ServerActions, ClientActions> client_handler) {
			this.client = client;
			this.client_handler = client_handler;
            mode = (client_handler == null)? JsonRpcMode.Client : JsonRpcMode.Server;
		}

		protected bool SendAndReceived(object args, [System.Runtime.CompilerServices.CallerMemberName] string member_name = "") {

			if (mode == JsonRpcMode.Server) {
				client_handler.Send(member_name, args);
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
