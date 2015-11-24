using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DtronixJsonRpc {
	class ClientConnectionRequest {
		public string Username { get; set; }
		public string Version { get; set; }
		public string AuthData { get; set; }
	}
}
