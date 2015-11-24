using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DtronixJsonRpc {
	class ClientConnectionResponse {
		public string Error { get; set; }
		public int ClientId { get; set; }

		public ClientConnectionResponse() {

		}
		public ClientConnectionResponse(string error) {
			Error = error;
		}
	}
}
