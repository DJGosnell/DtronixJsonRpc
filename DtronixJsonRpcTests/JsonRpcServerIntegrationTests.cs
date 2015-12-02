using DtronixJsonRpc;
using DtronixJsonRpcTests.Actions;
using Newtonsoft.Json;
using Newtonsoft.Json.Bson;
using Newtonsoft.Json.Linq;
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
	public class JsonRpcServerIntegrationTests : JsonRpcIntegrationTestBase {



		public JsonRpcServerIntegrationTests(ITestOutputHelper output) : base(output) { }

		[Fact]
		public async void Server_sends_abstract_data_to_client() {
			server.Configurations.AllowAnonymousConnections = true;
			server.Configurations.ServerData = JToken.Parse(@"{'logo':'http://my.logo/image.png'}");
			server.Start();

			client.OnReceiveConnectionInformation += (sender, e) => {
				Assert.Equal("http://my.logo/image.png", e.ServerData["logo"].ToString());

				CompleteTest();
			};

			client.Info.Username = null;
			client.Connect();

			await StartAndWaitClient();

		}

	}
}
