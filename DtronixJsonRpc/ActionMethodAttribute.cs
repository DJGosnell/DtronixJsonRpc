using System;

namespace DtronixJsonRpc {
	[System.AttributeUsage(AttributeTargets.Method, Inherited = false, AllowMultiple = true)]
	public class ActionMethodAttribute : Attribute {

		public JsonRpcSource Source { get; }
		// This is a positional argument
		/// <summary>
		/// Sets whether or not this is an actions that a JsonRpcConnector can call or not.
		/// </summary>
		/// <param name="source">Location this method should be called. Set to "unset" to allow method to be called on both client and server.</param>
		public ActionMethodAttribute(JsonRpcSource source) {
			Source = source;
		}

	}
}
