using System.Runtime.CompilerServices;

namespace DtronixJsonRpc {
	public abstract class JsonRpcActions<THandler>
		where THandler : ActionHandler<THandler>, new() {

		/// <summary>
		/// Name of the property that this action set is instanced on.
		/// </summary>
		/// <example>
		/// public class YourClientActions : JsonRpcActions&lt;YourActionHandler&gt; {
		///		// Action methods.
		/// }
		/// </example>
		private string reference_name;

		/// <summary>
		/// The client used to communicate with the other client.
		/// </summary>
		protected JsonRpcClient<THandler> Connector { get; }

		/// <summary>
		/// Base class for all JsonRpcActions.
		/// </summary>
		/// <param name="connector">Client connector for this instance.</param>
		/// <param name="member_name">Name of the property that this is instanced on. Do not set unless you are doing something fancy.</param>
		public JsonRpcActions(JsonRpcClient<THandler> connector, [CallerMemberName] string member_name = "") {
			this.Connector = connector;
			reference_name = member_name;
		}


		/// <summary>
		/// Method to allow sending of the passed parameters to the other connected client and notify it that data has been received.
		/// Used in conjunction with WaitForResult().
		/// </summary>
		/// <param name="args">Arguments to pass to the other client. Can be null.</param>
		/// <param name="id">Unique ID to this request.  This will be automatically assigned.</param>
		/// <param name="member_name">Name of this method.  Do not set unless you are doing something fancy.</param>
		/// <returns>True if code is going to be executing on other client and will be returning result.</returns>
		/// <example>
		/// [ActionMethod(JsonRpcSource.Server)]
		/// public async Task&lt;bool&gt; ReturnFalse(TestArgs args, string id = null) {
		/// 	if (RequestResult(args, ref id)) { return await Connector.WaitForResult&lt;bool&gt;(id); }
		///		// Code executed on other client.
		/// 	return false;
		/// }
		/// </example>
		protected bool RequestResult(object args, ref string id, [CallerMemberName] string member_name = "") {
			if (id == null) {
				id = Connector.GetNewRequestId();
				Connector.Send(new JsonRpcRequest(reference_name + "." + member_name, args, id));
				return true;
			} else {
				return false;
			}
		}

		/// <summary>
		/// Method to allow sending of the passed parameters to the other connected client and notify it that data has been received.
		/// Notifications do not have return values.
		/// </summary>
		/// <param name="args">Arguments to pass to the other client. Can be null</param>
		/// <param name="id">Unique ID to this request. Ignored for notifications.</param>
		/// <param name="member_name">Name of this method.  Do not set unless you are doing something fancy.</param>
		/// <returns>True if code is executing on other client.</returns>
		/// <example>
		/// [ActionMethod(JsonRpcSource.Client)]
		/// public void NotifyServer(TestArgs args, string id = null) {
		/// 	if (Notify(args, ref id)) {
		///			// Code executed on other client.
		/// 		return false;
		///		}
		/// }
		/// </example>
		protected bool Notify(object args, ref string id, [CallerMemberName] string member_name = "") {
			if (id == null) {
				Connector.Send(new JsonRpcRequest(reference_name + "." + member_name, args));
				return false;
			} else {
				return true;
			}
		}

	}
}
