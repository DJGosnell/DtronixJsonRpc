using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DtronixJsonRpc {
	public abstract class JsonRpcActions<THandler> 
		where THandler : ActionHandler<THandler>, new() {

		public JsonRpcConnector<THandler> Connector { get; }

		public JsonRpcActions(JsonRpcConnector<THandler> connector) {
			Connector = connector;
        }

		protected bool SendAndReceived(JsonRpcActionArgs args, string class_name, [System.Runtime.CompilerServices.CallerMemberName] string member_name = "") {
			if (args.Source == JsonRpcSource.Unset) {
				args.Source = Connector.Mode;
                Connector.Send(class_name + "." + member_name, args);
				return false;

			} else {
				return true;
			}

		}

	}
}
