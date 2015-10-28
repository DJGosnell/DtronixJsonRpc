using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DtronixJsonRpc {
	public class JsonRpcActionCall<THandler, TArg>
		where THandler : ActionHandler<THandler>, new() {

		public string Id {
			get {
				return _Request.Id;
			}
		}

		private JsonRpcParam<TArg> _Request;

		public JsonRpcParam<TArg> Request {
			get { return _Request; }
			set { _Request = value; }
		}


		private JsonRpcClient<THandler> _Client;

		public JsonRpcClient<THandler> Client {
			get { return _Client; }
			set { _Client = value; }
		}


		public void Respond(TArg args) {
			_Client.Send(new JsonRpcParam<TArg>(args) {
				Method = _Request.Method,
				Id = _Request.Id
			});
		}

	}
}
