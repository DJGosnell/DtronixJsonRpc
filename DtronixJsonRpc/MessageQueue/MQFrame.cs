using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DtronixJsonRpc.MessageQueue {
	// ReSharper disable once InconsistentNaming
	public class MQFrame : IDisposable {

		private MemoryStream input_stream; 

		private byte[] data;
		private int frame_length = -1;


		/// <summary>
		/// Information about this frame and how it relates to other frames.
		/// </summary>
		public MQFrameType FrameType { get; internal set; } = MQFrameType.Unset;

		/// <summary>
		/// Total bytes that this frame contains.
		/// </summary>
		public int FrameLength => frame_length;

		/// <summary>
		/// True if this frame data has been completely read.
		/// </summary>
		public bool FrameComplete => data != null;

		/// <summary>
		/// Bytes this frame contains.
		/// </summary>
		public byte[] Data => data;

		private const int HeaderLength = 5;

		public MQFrame() {
			input_stream = new MemoryStream();
		}

		public MQFrame(byte[] bytes, MQFrameType type) {
			data = bytes;
			frame_length = bytes.Length;
			FrameType = type;
		}



		public int Write(byte[] bytes, int offset, int count) {
			input_stream.Write(bytes, offset, count);
			int over_read = 0;

			if (FrameType == MQFrameType.Unset) {
				FrameType = (MQFrameType) bytes[offset];
			}

			// Read the length from the stream if there are enough bytes.
			if (input_stream.Length >= HeaderLength) {
				var frame_len = new byte[4];
				var original_position = input_stream.Position;

				// Set the position of the stream to the location of the length.
				input_stream.Position = 1;
				input_stream.Read(frame_len, 0, frame_len.Length);
				frame_length = BitConverter.ToInt32(frame_len, 0);
				data = new byte[frame_length];

				if (frame_length > 1024*16) {
					throw new InvalidDataException($"Frame size is {frame_length} while the maximum size for frames is 30KB.");
				}

				// Set the stream back to the position it was at to begin with.
				input_stream.Position = original_position;
			}

			// We have over-read into another frame  Setup a new frame.
			if (FrameLength != -1 && input_stream.Length - HeaderLength > FrameLength) {
				over_read = (int)input_stream.Length - HeaderLength - FrameLength;
				input_stream.SetLength(input_stream.Length - over_read);
			}

			if (FrameLength != -1 && input_stream.Length - HeaderLength == FrameLength) {
				input_stream.Position = HeaderLength;
				input_stream.Read(data, 0, FrameLength);
				input_stream.Close();
				input_stream = null;
			}

			return over_read;


		}

		public void Dispose() {
			input_stream?.Dispose();
			data = null;
		}
	}
}
