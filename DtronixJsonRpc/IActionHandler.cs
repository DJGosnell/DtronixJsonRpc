namespace DtronixJsonRpc {
	public interface IActionHandler {
		JsonRpcConnector<IActionHandler> Connector { get; set; }
		void ExecuteAction(string method, object obj);
	}
}