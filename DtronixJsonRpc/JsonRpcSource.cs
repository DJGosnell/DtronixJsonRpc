using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DtronixJsonRpc {
	public enum JsonRpcSource {
		Unset = 1 << 0,
		Server = 1 << 1,
		Client = 1 << 2
	}
}
