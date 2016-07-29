using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DtronixJsonRpc.MessageQueue {
	public class MQFrame {
		private MemoryStream input_stream = new MemoryStream();
		public int FrameLength { get; private set; } = -1;
		public bool Complete => data != null;
		private byte[] data;
		public MQFrame() {
			
		}

		public int Write(byte[] bytes, int offset, int count) {
			input_stream.Write(bytes, offset, count);
			int over_read = 0;

			// Read the length from the stream if there are enough bytes.
			if (input_stream.Length >= 4) {
				byte[] frame_length = new byte[4];
				input_stream.Read(frame_length, 0, 4);
				FrameLength = BitConverter.ToInt32(frame_length, 0);
			}

			// We have over-read into another frame  Setup a new frame.
			if (FrameLength != -1 && input_stream.Length - 4 > FrameLength) {
				over_read = (int)input_stream.Length - 4 - FrameLength;
				input_stream.SetLength(input_stream.Length - over_read);
			}

			if (FrameLength != -1 && input_stream.Length >= FrameLength - 4) {
				input_stream.Read(data, 4, FrameLength);
			}

			return over_read;


		}
	}
}
