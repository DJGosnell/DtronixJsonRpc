using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

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
