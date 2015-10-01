using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace DtronixJsonRpc {
	public class ActionHandler<THandler> 
		where THandler : ActionHandler<THandler>, new() {

		public JsonRpcConnector<THandler> Connector { get; set; }

		/*private Dictionary<string, JsonRpcActions<IActionHandler>> loaded_actions = new Dictionary<string, JsonRpcActions<IActionHandler>>();

		public void AddActions(string name, JsonRpcActions<IActionHandler> actions) {
			loaded_actions.Add( actions)
        }*/

		private static Dictionary<string, MethodInfo> method_cache = new Dictionary<string, MethodInfo>();
		private static object method_cache_lock = new object();

		private Dictionary<string, object> instance_cache = new Dictionary<string, object>();

		

		public void ExecuteAction(string method, object obj) {
			MethodInfo method_info;
			object instance_class;

			if (method_cache.TryGetValue(method, out method_info) == false || instance_cache.TryGetValue(method, out instance_class) == false) {
				var call_parts = method.Split('.');
				var this_type = GetType();
				var type_property = this_type.GetProperty(call_parts[0]);
                instance_class = type_property.GetValue(this);

				var typ = instance_class.GetType();
				method_info = typ.GetMethod(call_parts[1]);
				lock (method_cache_lock) {
					method_cache.Add(method, method_info);
					instance_cache.Add(method, instance_class);
                }
			}

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
				method_info.Invoke(instance_class, args);

			} catch (Exception e) {
				throw e;
			}

		}
	}
}
