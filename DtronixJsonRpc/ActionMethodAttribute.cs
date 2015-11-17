using System;

namespace DtronixJsonRpc {
	/// <summary>
	/// Attribute to define where this action should be executed. Must be set on all callable actions.
	/// </summary>
	[System.AttributeUsage(AttributeTargets.Method, Inherited = false, AllowMultiple = false)]
	public class ActionMethodAttribute : Attribute {

		/// <summary>
		/// Location this action is to be called.
		/// Location this method should be called. Set to "unset" to allow method to be called on both client and server.
		/// </summary>
		public JsonRpcSource Source { get; }


		/// <summary>
		/// Sets whether or not this is an actions that a JsonRpcConnector can call or not.
		/// </summary>
		/// <param name="source">Location this method should be called. Set to "unset" to allow method to be called on both client and server.</param>
		public ActionMethodAttribute(JsonRpcSource source) {
			Source = source;
		}

	}
}
