using System.Runtime.CompilerServices;

namespace DtronixJsonRpc {
	public abstract class JsonRpcActions<THandler> 
		where THandler : ActionHandler<THandler>, new() {

		private string reference_name;

		protected JsonRpcClient<THandler> Connector { get; }

		public JsonRpcActions(JsonRpcClient<THandler> connector, [CallerMemberName] string member_name = "") {
			this.Connector = connector;
			reference_name = member_name;
		}

		protected bool SendAndReturnResult<T>(T args, ref string id, [CallerMemberName] string member_name = "") {
			if (id != null) {
				return false;
			} else {
				id = Connector.GetNewRequestId();
				Connector.Send(new JsonRpcParam(reference_name + "." + member_name, args, id));
				return true;
			}
		}

	}
}
