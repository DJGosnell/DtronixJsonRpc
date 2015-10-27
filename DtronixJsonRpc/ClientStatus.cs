using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DtronixJsonRpc {
	public enum ClientStatus {
		Unset = 1 << 0,
		Connected = 1 << 1,
		Connecting = 1 << 2,
		Authorized = 1 << 3,
		Disconnected = 1 << 4,
		Disconnecting = 1 << 5,
		Away = 1 << 6,
	}
}
