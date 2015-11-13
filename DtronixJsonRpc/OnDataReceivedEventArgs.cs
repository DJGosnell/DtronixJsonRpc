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

		public OnDataReceivedEventArgs(JToken data) {
			Data = data;
		}
	}
}
