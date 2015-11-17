using DtronixJsonRpc;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace DtronixJsonRpcTests.Actions {
	public class TestServerActions : JsonRpcActions<TestActionHandler> {

		new public JsonRpcClient<TestActionHandler> Connector { get { return base.Connector; } }

		public TestServerActions(JsonRpcClient<TestActionHandler> connector, [CallerMemberName] string member_name = "") : base(connector, member_name) {

		}


		public class TestArgs {
			public long RandomLong { get; set; }
		}

		[ActionMethod(JsonRpcSource.Server)]
		public async Task<bool> TestReturnTrue(TestArgs args, string id = null) {
			if (SendAndReturnResult(args, ref id)) { return await Connector.WaitForResult<bool>(id); }

			return true;


		}

	}
}