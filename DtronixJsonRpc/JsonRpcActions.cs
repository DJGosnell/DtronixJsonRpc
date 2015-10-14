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
        protected readonly JsonRpcConnector<THandler> connector;

        //public JsonRpcConnector<THandler> Connector { get; }

		public JsonRpcActions(JsonRpcConnector<THandler> connector, [CallerMemberName] string member_name = "") {
			this.connector = connector;
			reference_name = member_name;
        }

        protected bool SendAndReceived(JsonRpcActionArgs args, [CallerMemberName] string member_name = "") {
			return SendAndReceived(args, reference_name, member_name);
		}

		protected bool SendAndReceived(JsonRpcActionArgs args, string class_name, [CallerMemberName] string member_name = "") {
			if (args.Source == JsonRpcSource.Unset) {
				args.Source = connector.Mode;
                connector.Send(class_name + "." + member_name, args);
				return false;

			} else {
				return true;
			}

		}

	}
}
