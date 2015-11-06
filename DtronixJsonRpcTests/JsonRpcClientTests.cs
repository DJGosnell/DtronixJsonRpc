﻿using DtronixJsonRpc;
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
	public class JsonRpcClientTests : IDisposable {
		private ITestOutputHelper output;

		public static int port = 2827;

		private ManualResetEvent wait = new ManualResetEvent(false);

		private TcpListener server;
		private JsonRpcClient<TestActionHandler> client;
		private TcpClient server_client;
		private NetworkStream server_client_stream;
		private BsonWriter writer;
		private BsonReader reader;
		private JsonSerializer serializer = new JsonSerializer();
		private Task server_task;
		private Task client_task;

		public JsonRpcClientTests(ITestOutputHelper output) {
			this.output = output;

			server = TcpListener.Create(Interlocked.Increment(ref port));
			server.Start();



			client = JsonRpcClient<TestActionHandler>.CreateClient("localhost", ((IPEndPoint)server.LocalEndpoint).Port, JsonRpcServerConfigurations.TransportMode.Bson);

			client.OnAuthenticationRequest += (sender, e) => {
				e.Data = "ArbitraryAuthText";
			};
		}

		private void Client_OnAuthenticationRequest(JsonRpcClient<TestActionHandler> sender, ConnectorAuthenticationEventArgs e) {
			throw new NotImplementedException();
		}

		[Fact]
		public async void Connect_should_connect() {

			server_task = new Task(() => {
				CreateServerClient();
				Assert.NotNull(server_client_stream);
				DisconnectClient();
			});

			client_task = new Task(() => { client.Connect(); });

			await StartAndWaitClient();
		}

		[Fact]
		public async void Connect_should_authenticate() {

			server_task = new Task(() => {
				CreateServerClient();
				Assert.True(Authenticate("ArbitraryAuthText"), "Successfully authenticated");
				DisconnectClient();

			});

			client_task = new Task(() => { client.Connect(); });

			await StartAndWaitClient();
		}

		[Fact]
		public async void Connect_should_fail_authentication() {

			server_task = new Task(() => {
				CreateServerClient();
				Assert.False(Authenticate("FakeAuthData"), "Successfully authenticated");
				DisconnectClient();

			});

			client_task = new Task(() => { client.Connect(); });

			await StartAndWaitClient();

		}

		private JToken Read() {
			// Move the head to the next token in the stream.
			reader.Read();

			// Read the entire object from the stream forward.  Stops at the end of this object.
			return JToken.ReadFrom(reader);
		}

		private void Send<T>(JsonRpcParam<T> args) {
			serializer.Serialize(writer, args);
			writer.Flush();
		}

		private bool Authenticate(string auth_string) {
			var client_info = Read().ToObject<JsonRpcParam<ClientInfo>>();
			Send(new JsonRpcParam<int>(null, 1));
			var authentication_text = Read().ToObject<JsonRpcParam<string>>();

			if (auth_string == authentication_text.Args) {
				Send(new JsonRpcParam<string>("rpc.OnAuthenticationSuccess"));
				return true;
			} else {
				Send(new JsonRpcParam<string>("rpc.OnAuthenticationFailure", "Failed Validation"));
				return false;
			}
		}

		private void CreateServerClient() {
			server_client = server.AcceptTcpClient();
			server_client_stream = server_client.GetStream();
			writer = new BsonWriter(server_client_stream);
			reader = new BsonReader(server_client_stream);
			writer.Formatting = Formatting.None;
			reader.SupportMultipleContent = true;
		}

		private void DisconnectClient() {
			Send(new JsonRpcParam<ClientInfo[]>("rpc.OnDisconnect", new ClientInfo[] { new ClientInfo() {
				DisconnectReason = "Test completed"
			}}));
			server_client.Close();
			server.Stop();
		}

		public async Task<bool> StartAndWaitClient() {
			server_task.Start();
			client_task.Start();

			if (await Task.WhenAny(server_task, Task.Delay(5000)) != server_task) {
				client.Disconnect("Test failed to complete within the time limitation.");
				return false;
			}
			client.Disconnect("Test failed to complete within the time limitation.");
			return true;
		}

		public void Dispose() {


			server_task.Exception?.Handle(ex => {
				throw ex;
			});

			client_task.Exception?.Handle(ex => {
				throw ex;
			});
		}
	}
}
