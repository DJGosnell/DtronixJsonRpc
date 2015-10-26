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

		protected JsonRpcConnector<THandler> Connector { get; }

		public JsonRpcActions(JsonRpcConnector<THandler> connector, [CallerMemberName] string member_name = "") {
			this.Connector = connector;
			reference_name = member_name;
        }

		protected bool SendAndReceived<T>(JsonRpcParam<T> args, [CallerMemberName] string member_name = "") {
			if(args == null) {
				args = new JsonRpcParam<T>();
            }

			if (args.Method == null) {
				args.Method = reference_name + "." + member_name;
                Connector.Send(args);

				return false;

			} else {
				return true;
			}

		}

	}
}
