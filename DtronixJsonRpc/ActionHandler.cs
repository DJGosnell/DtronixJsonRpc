using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DtronixJsonRpc {
	public class ActionHandler<THandler> : IActionHandler {

		public JsonRpcConnector<THandler> Connector { get; set; }

		/*private Dictionary<string, JsonRpcActions<IActionHandler>> loaded_actions = new Dictionary<string, JsonRpcActions<IActionHandler>>();

		public void AddActions(string name, JsonRpcActions<IActionHandler> actions) {
			loaded_actions.Add( actions)
        }*/

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
