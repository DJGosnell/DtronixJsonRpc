using DtronixJsonRpc;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Runtime.CompilerServices;

namespace DtronixJsonRpcTests {
	public class TestClientMethodCalledEventArgs : EventArgs {
		public Type Type { get; set; }

		public string Method { get; set; }
		public JsonRpcActionArgs Arguments { get; set; }

		public TestClientMethodCalledEventArgs(JsonRpcActionArgs args, Type type, [CallerMemberName] string member_name = "") {
			Type = type;
			Arguments = args;
			Method = member_name;
		}

	}
}
