using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DtronixJsonRpc {
	public abstract class JsonRpcActions<THandler> where THandler : IActionHandler {

		public JsonRpcConnector<THandler> Connector { get; }

		public JsonRpcActions(JsonRpcConnector<THandler> connector) {
			Connector = connector;
        }

		protected bool SendAndReceived(JsonRpcActionArgs args, string class_name, [System.Runtime.CompilerServices.CallerMemberName] string member_name = "") {
			if (Connector.Mode == JsonRpcSource.Server) {
				if (args.Source == JsonRpcSource.Client) {
					return true;
				}

				Connector.Send(member_name, args);
				return false;
			} else {
				if (args.Source == JsonRpcSource.Server) {
					return true;
				}

				Connector.Send(member_name, args);
				return false;
			}
		}

	}
}
