using Newtonsoft.Json;
using System.ComponentModel;

namespace DtronixJsonRpc {

	/// <summary>
	/// Request based upon JSON RPC 2.0 Specifications.
	/// </summary>
	/// <seealso cref="http://www.jsonrpc.org/specification"/>
	public class JsonRpcRequest {

		/// <summary>
		/// A String specifying the version of the JSON-RPC protocol. MUST be exactly "2.0".
		/// </summary>
		[JsonProperty("jsonrpc")]
		public string JsonRPC { get; } = "2.0";

		/// <summary>
		/// An identifier established by the Client that MUST contain a String, Number, or NULL value if included. If it is not included it is assumed to be a notification. The value SHOULD normally not be Null [1] and Numbers SHOULD NOT contain fractional parts [2]
		/// </summary>
		[JsonProperty("id")]
		public string Id { get; set; }

		/// <summary>
		/// A String containing the name of the method to be invoked.
		/// Method names that begin with the word rpc followed by a period character (U+002E or ASCII 46) are reserved for rpc-internal methods and extensions and MUST NOT be used for anything else.
		/// </summary>
		[JsonProperty("method")]
		public string Method { get; set; }

		/// <summary>
		/// (RESPONSE ONLY)
		/// This member is REQUIRED on success.
		/// This member MUST NOT exist if there was an error invoking the method.
		/// The value of this member is determined by the method invoked on the Server.
		/// </summary>
		[JsonProperty("result")]
		public object Result { get; set; }

		/// <summary>
		/// (RESPONSE ONLY)
		/// This member is REQUIRED on error.
		/// This member MUST NOT exist if there was no error triggered during invocation.
		/// The value for this member MUST be an Object as defined in section 5.1.
		/// </summary>
		[JsonProperty("error")]
		public JsonRpcError Error { get; set; }

		/// <summary>
		/// (REQUEST ONLY)
		/// A Structured value that holds the parameter values to be used during the invocation of the method. This member MAY be omitted.
		/// </summary>
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
