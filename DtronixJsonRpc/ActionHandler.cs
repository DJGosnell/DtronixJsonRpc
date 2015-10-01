using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace DtronixJsonRpc {
	public class ActionHandler<THandler> 
		where THandler : ActionHandler<THandler>, new() {

		private class CalledMethodInfo {
			public MethodInfo method_info;
			public ParameterInfo[] parameter_info;
			public ActionMethodAttribute attribute_info;
		}

		public JsonRpcConnector<THandler> Connector { get; set; }

		/*private Dictionary<string, JsonRpcActions<IActionHandler>> loaded_actions = new Dictionary<string, JsonRpcActions<IActionHandler>>();

		public void AddActions(string name, JsonRpcActions<IActionHandler> actions) {
			loaded_actions.Add( actions)
        }*/

		private static Dictionary<string, CalledMethodInfo> called_method_cache = new Dictionary<string, CalledMethodInfo>();
		private static Dictionary<string, MethodInfo> method_cache = new Dictionary<string, MethodInfo>();
		private static Dictionary<string, ParameterInfo[]> method_parameter_cache = new Dictionary<string, ParameterInfo[]>();
		private static object method_cache_lock = new object();

		private Dictionary<string, object> instance_cache = new Dictionary<string, object>();

		

		public void ExecuteAction(string method, object obj) {
			CalledMethodInfo called_method_info;
			ParameterInfo[] parameter_info;
			object instance_class;
			var call_parts = method.Split('.');
			object[] args = new object[] { obj };

			// Get the class.
			if (instance_cache.TryGetValue(method, out instance_class) == false) {
				var this_type = GetType();
				var type_property = this_type.GetProperty(call_parts[0]);

				if (type_property == null) {
					throw new InvalidOperationException("Method requested by the server does not exist.");
				}

				instance_class = type_property.GetValue(this);

				lock (method_cache_lock) {
					instance_cache.Add(method, instance_class);
				}
			}

			if (called_method_cache.TryGetValue(method, out called_method_info) == false) {

				// Get the method.
				var typ = instance_class.GetType();
				called_method_info.method_info = typ.GetMethod(call_parts[1]);

				if (called_method_info.method_info == null) {
					throw new InvalidOperationException("Method requested by the server does not exist.");
				}

				// Get the method parameters.
				called_method_info.parameter_info = called_method_info.method_info.GetParameters();

				// Get the attributes.
				called_method_info.attribute_info = called_method_info.method_info.GetCustomAttribute<ActionMethodAttribute>();

				
               if (called_method_info.attribute_info == null) {
					throw new InvalidOperationException("Method requested by the server is not allowed to be called.");
				}

				lock (method_cache_lock) {
					called_method_cache.Add(method, called_method_info);
                }
			}

			if(called_method_info.attribute_info.Source != Connector.Mode) {
				throw new InvalidOperationException("Method called is not allowed to be called in a " + Connector.Mode.ToString());
			}

			try {
				called_method_info.method_info.Invoke(instance_class, args);

			} catch (Exception e) {
				throw e;
			}

		}
	}
}
