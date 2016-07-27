using DtronixJsonRpc;
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
using NetMQ;
using NetMQ.Sockets;
using Xunit;
using Xunit.Abstractions;

namespace DtronixJsonRpcTests {
	public class JsonRpcClientIntegrationTests {

		[Fact]
		public async void ReturnTrue_multiple_calls_succeed() {
			var configs = new JsonRpcServerConfigurations();
			var server = new JsonRpcServer(configs);

			server.Start();


			var dealer = new DealerSocket();
			dealer.Connect(configs.BindingAddress);

			dealer.TrySendFrame(new byte[] {1, 2, 3, 4});


			Thread.Sleep(100000);

		}



	}
}
