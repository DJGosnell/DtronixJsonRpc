using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NLog;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace DtronixJsonRpc {
	public class ActionHandler<THandler> 
		where THandler : ActionHandler<THandler>, new() {

		private static Logger logger = LogManager.GetCurrentClassLogger();

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

			

		private static ConcurrentDictionary<string, CalledMethodInfo> called_method_cache = new ConcurrentDictionary<string, CalledMethodInfo>();
		private static object method_cache_lock = new object();

		private Dictionary<string, object> instance_cache = new Dictionary<string, object>();

		

		public void ExecuteAction(string method, JToken data) {
			CalledMethodInfo called_method_info;
			object instance_class;

			var call_parts = method.Split('.');

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
				called_method_info = new CalledMethodInfo();

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
					throw new InvalidOperationException("Method called is not allowed to be called.");
				}

				called_method_cache.TryAdd(method, called_method_info);

			}

			// Use the first parameter.
			Type parameter_type = called_method_info.parameter_info[0]?.ParameterType;

			if(parameter_type == null) {
				throw new InvalidOperationException("Called method does not have a parameter which to pass the data to.");
			}

			if (called_method_info.attribute_info.Source != JsonRpcSource.Unset && called_method_info.attribute_info.Source != Connector.Mode) {
				throw new InvalidOperationException("Method called is not allowed to be called in a " + Connector.Mode.ToString());
			}

			try {
				called_method_info.method_info.Invoke(instance_class, new object[] { data.ToObject(parameter_type) });

			} catch (Exception e) {
				throw e;
			}

		}
	}
}
