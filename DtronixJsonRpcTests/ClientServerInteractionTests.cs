using System;
using Xunit;
using DtronixJsonRpc;
using Xunit.Abstractions;

namespace DtronixJsonRpcTests {
	public class ClientServerInteractionTests {
		private readonly ITestOutputHelper output;

		public ClientServerInteractionTests(ITestOutputHelper output) {
			this.output = output;
		}


		[Fact]
		public void ServerStartsAndClientConnects() {
			var server = new JsonRpcServer
		}
	}
}
