using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DtronixJsonRpc {
	public enum ClientStatus {
		Requesting = 0,
		Ready = 1 << 1,
		Away = 1 << 2,
		Connected = 1 << 3,
		Connecting = 1 << 4,
		Disconnected = 1 << 5
	}
}
