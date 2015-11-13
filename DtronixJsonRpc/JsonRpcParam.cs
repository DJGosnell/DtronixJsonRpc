using Newtonsoft.Json;
using System.ComponentModel;

namespace DtronixJsonRpc {
	public class JsonRpcParam<T> {
		[JsonProperty("jsonrpc")]
		internal string JsonRPC { get; } = "2.0";

		[JsonProperty("method")]
		internal string Method { get; set; }

		[JsonProperty("args")]
		public T Args { get; set; }

		public event PropertyChangedEventHandler PropertyChanged;

		public JsonRpcParam() {

		}

		public JsonRpcParam(T args) {
			Args = args;
		}

		internal JsonRpcParam(string method) {
			Method = method;
		}

		internal JsonRpcParam(string method, T args) {
			Method = method;
			Args = args;
		}

	}
}
