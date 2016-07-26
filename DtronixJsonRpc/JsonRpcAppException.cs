using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;

namespace DtronixJsonRpc {
	public class JsonRpcAppException : Exception {
		public JsonRpcAppException() {
		}

		public JsonRpcAppException(string message) : base(message) {
		}

		public JsonRpcAppException(string message, Exception innerException) : base(message, innerException) {
		}

		protected JsonRpcAppException(SerializationInfo info, StreamingContext context) : base(info, context) {
		}
	}
}
