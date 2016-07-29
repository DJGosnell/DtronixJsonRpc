using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DtronixJsonRpc.MessageQueue {
	public enum MQFrameType : byte {

		/// <summary>
		/// This frame type has not been determined yet.
		/// </summary>
		Unset,

		/// <summary>
		/// This frame is part of a larger message.
		/// </summary>
		More,

		/// <summary>
		/// This frame is the last part of a message.
		/// </summary>
		Last
	}
}
