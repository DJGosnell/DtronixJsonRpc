using DtronixJsonRpc;
using DtronixJsonRpcTests.Actions;
using Newtonsoft.Json;
using Newtonsoft.Json.Bson;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using Xunit.Abstractions;

namespace DtronixJsonRpcTests {
	public class JsonRpcClientIntegrationTests {



		public JsonRpcClientIntegrationTests(ITestOutputHelper output) : base(output) { }

		[Fact]
		public async void Ping_does_not_time_out() {
			

		}



	}
}
