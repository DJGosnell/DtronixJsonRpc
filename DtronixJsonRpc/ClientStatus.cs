namespace DtronixJsonRpc {
	public enum ClientStatus {
		Unset = 1 << 0,
		Connected = 1 << 1,
		Connecting = 1 << 2,
		Disconnected = 1 << 3,
		Disconnecting = 1 << 4
	}
}
