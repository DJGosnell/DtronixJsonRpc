using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DtronixJsonRpc {
	
	/// <summary>
	/// Contains the parsed JToken data.
	/// </summary>
	internal class OnDataReceivedEventArgs : EventArgs {
		public JToken Data { get; set; }

		/// <summary>
		/// If set to true, the client/server will ignore parsing the rest of this request and will not call the associated action.
		/// </summary>
		public bool Handled { get; set; }

		public OnDataReceivedEventArgs(JToken data) {
			Data = data;
		}
	}
}
