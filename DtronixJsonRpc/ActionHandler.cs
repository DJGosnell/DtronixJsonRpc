using Newtonsoft.Json.Linq;
using NLog;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;

namespace DtronixJsonRpc {

	/// <summary>
	/// Base class for all action handlers.
	/// </summary>
	/// <typeparam name="THandler">The inherited class of this base ActionHandler.</typeparam>
	/// <example>
	/// public class YourActionHandler : ActionHandler&lt;TestActionHandler&gt; {
	///		private YourClientActions _YourClientActions = null;
	///		public YourClientActions YourClientActions {
	///			get {
	///				return _YourClientActions ?? (_YourClientActions = new YourClientActions(Connector));
	///			}
	///		}
	/// }
	/// </example>
	public abstract class ActionHandler<THandler>
		where THandler : ActionHandler<THandler>, new() {


		/// <summary>
		/// Version of this client/server.
		/// </summary>
		public abstract string Version { get; }

		private static Logger logger = LogManager.GetCurrentClassLogger();

		private class CalledMethodInfo {
			public MethodInfo method_info;
			public ParameterInfo[] parameter_info;
			public ActionMethodAttribute attribute_info;
		}

		public JsonRpcClient<THandler> Connector { get; set; }

		private static ConcurrentDictionary<string, CalledMethodInfo> called_method_cache = new ConcurrentDictionary<string, CalledMethodInfo>();
		private static object method_cache_lock = new object();

		private Dictionary<string, object> instance_cache = new Dictionary<string, object>();

		internal ConcurrentDictionary<string, CancellationTokenSource> active_cancellable_actions = new ConcurrentDictionary<string, CancellationTokenSource>();

		public void ExecuteAction(string method, JToken data, string id) {
			CalledMethodInfo called_method_info;
			CancellationTokenSource cancel_source = new CancellationTokenSource();
			object instance_class;

			var call_parts = method.Split('.');

			// Get the class.
			if (instance_cache.TryGetValue(method, out instance_class) == false) {
				var this_type = GetType();
				var type_property = this_type.GetProperty(call_parts[0]);

				if (type_property == null) {
					throw new InvalidOperationException("Method requested (" + method + ") by the server does not exist.");
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
					throw new InvalidOperationException("Method requested (" + method + ") by the server does not exist.");
				}

				// Get the method parameters.
				called_method_info.parameter_info = called_method_info.method_info.GetParameters();

				// Get the attributes.
				called_method_info.attribute_info = called_method_info.method_info.GetCustomAttribute<ActionMethodAttribute>();


				if (called_method_info.attribute_info == null) {
					throw new InvalidOperationException("Method called (" + method + ") is not allowed to be called.");
				}

				called_method_cache.TryAdd(method, called_method_info);

			}

			// Use the first parameter.
			Type parameter_type = called_method_info.parameter_info[0]?.ParameterType;

			if (parameter_type == null) {
				throw new InvalidOperationException("Method called (" + method + ") does not have a parameter which to pass the data to.");
			}

			if (called_method_info.attribute_info.Source != JsonRpcSource.Unset && called_method_info.attribute_info.Source != Connector.Mode) {
				throw new InvalidOperationException("Method called (" + method + ") is not allowed to be called in a " + Connector.Mode.ToString());
			}

			try {


				// Determine what kind of parameters we have.
				object[] parameters;
				if (called_method_info.parameter_info.Length == 1) {
					// If there is only one parameter, then it is the call ID.
					parameters = new object[] { id };

				} else if (called_method_info.parameter_info.Length == 2) {
					// Two parameters means the parameters and the call ID.
					parameters = new object[] { data["params"].ToObject(parameter_type), id };

				} else if (called_method_info.parameter_info.Length == 3) {
					// Three parameters means the parameters, the cancellation token and the call ID.

					// Add it to the list of active actions.
					active_cancellable_actions.TryAdd(id, cancel_source);

					parameters = new object[] { data["params"].ToObject(parameter_type), cancel_source.Token, id };

				} else {
					throw new InvalidOperationException("Did not pass the minimum number of parameters.");
				}

				Task.Run(() => {
					try {
						// Invoke the method and see if we have a return value.
						object result = called_method_info.method_info.Invoke(instance_class, parameters);

						// If the method return value was not void, then send the result back to the other party.
						if (called_method_info.method_info.ReturnType != typeof(void) && ((dynamic)result).Exception == null) {
							Connector.Send(new JsonRpcRequest() {
								Result = ((dynamic)result).Result,
								Id = id
							});
						}
					} finally {

						// Try to remove the cancellation source from the list.
						CancellationTokenSource source;
						active_cancellable_actions.TryRemove(id, out source);
					}
				});

			} catch (Exception e) {
				throw e;

			}

		}
	}
}
