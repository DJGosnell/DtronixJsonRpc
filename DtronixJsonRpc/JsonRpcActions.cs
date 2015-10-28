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

		protected bool SendAndReceived<T>(T args, bool received, [CallerMemberName] string member_name = "") {
			if (received) {
				return true;
			} else { 
				Connector.Send(new JsonRpcParam<T>(reference_name + "." + member_name, args));
				return false;
			}
		}

	}
}
