using Newtonsoft.Json;
using System.ComponentModel;

namespace DtronixJsonRpc {
	public class JsonRpcRequest {
		[JsonProperty("jsonrpc")]
		internal string JsonRPC { get; } = "2.0";

		[JsonProperty("method")]
		internal string Method { get; set; }

		[JsonProperty("result")]
		internal object Result { get; set; }

		[JsonProperty("id")]
		internal string Id { get; set; }

		[JsonProperty("params")]
		public object Params { get; set; }

		public event PropertyChangedEventHandler PropertyChanged;

		public JsonRpcRequest() {

		}

		public JsonRpcRequest(object parameters) {
			Params = parameters;
		}

		internal JsonRpcRequest(string method) {
			Method = method;
		}

		internal JsonRpcRequest(string method, object parameters) {
			Method = method;
			Params = parameters;
		}

		internal JsonRpcRequest(string method, object parameters, string id) {
			Method = method;
			Params = parameters;
			Id = id;
		}

	}
}
