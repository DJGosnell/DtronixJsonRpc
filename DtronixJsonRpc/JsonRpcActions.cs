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

		protected bool RequestResult<T>(T args, ref string id, [CallerMemberName] string member_name = "") {
			if (id == null) {
				id = Connector.GetNewRequestId();
				Connector.Send(new JsonRpcRequest(reference_name + "." + member_name, args, id));
				return true;
			} else {
				return false;
			}
		}

		protected bool Notify<T>(T args, ref string id, [CallerMemberName] string member_name = "") {
			if (id == null) {
				Connector.Send(new JsonRpcRequest(reference_name + "." + member_name, args));
				return false;
			} else {
				return true;
			}
		}

	}
}
