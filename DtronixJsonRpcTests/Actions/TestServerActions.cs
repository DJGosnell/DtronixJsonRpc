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
		public async Task<bool> ReturnTrue(TestArgs args, string id = null) {
			if (RequestResult(args, ref id)) { return await Connector.WaitForResult<bool>(id); }

			return true;


		}

		[ActionMethod(JsonRpcSource.Server)]
		public async Task<bool> ReturnFalse(TestArgs args, string id = null) {
			if (RequestResult(args, ref id)) { return await Connector.WaitForResult<bool>(id); }

			return false;


		}

		[ActionMethod(JsonRpcSource.Server)]
		public async Task<bool> ReturnTrueWithoutParams(string id = null) {
			if (RequestResult(null, ref id)) { return await Connector.WaitForResult<bool>(id); }

			return true;


		}

		[ActionMethod(JsonRpcSource.Server)]
		public void NotifyServer(TestArgs args, string id = null) {
			if (Notify(args, ref id)) {
				// Do stuff

			}

		}



	}
}