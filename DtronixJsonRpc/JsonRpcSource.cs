namespace DtronixJsonRpc {

	/// <summary>
	/// The location that an event occurs.
	/// </summary>
	public enum JsonRpcSource {
		/// <summary>
		/// Unknown location.
		/// </summary>
		Unset = 1 << 0,

		/// <summary>
		/// Server source.
		/// </summary>
		Server = 1 << 1,

		/// <summary>
		/// Client source.
		/// </summary>
		Client = 1 << 2
	}
}
