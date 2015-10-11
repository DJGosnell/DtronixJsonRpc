using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DtronixJsonRpc {
	public abstract class JsonRpcActions<THandler> 
		where THandler : ActionHandler<THandler>, new() {

		private string this_class_name;
        protected readonly JsonRpcConnector<THandler> connector;

        //public JsonRpcConnector<THandler> Connector { get; }

		public JsonRpcActions(JsonRpcConnector<THandler> connector) {
			this.connector = connector;
        }

        protected bool SendAndReceived(JsonRpcActionArgs args, [System.Runtime.CompilerServices.CallerMemberName] string member_name = "") {
			if(this_class_name == null) {
				this_class_name = GetType().Name.Split('`')[0];
			}
			return SendAndReceived(args, this_class_name, member_name);

		}

		protected bool SendAndReceived(JsonRpcActionArgs args, string class_name, [System.Runtime.CompilerServices.CallerMemberName] string member_name = "") {
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
