namespace RabbitMqNext.Internals
{
	using System;
	using System.IO;
	using RingBuffer;

	/// <summary>
	/// The content of a message in AMQP can span multiple bodies. 
	/// This stream impl abstracts this fact away from the consumer.
	/// </summary>
	public class MultiBodyStreamWrapper : BaseLightStream
	{
		private readonly RingBufferStreamAdapter _innerStream;
		private readonly byte[] _frameBuffer = new byte[8];
		private int _currentFrameLenLeft;
		private long _remainingTotalBody;
		private int _position;

		public MultiBodyStreamWrapper(RingBufferStreamAdapter innerStream, int firstFrameLen, long totalBodyLen)
		{
			_innerStream = innerStream;
			_currentFrameLenLeft = firstFrameLen;
			_remainingTotalBody = totalBodyLen - firstFrameLen;
		}

		public override BaseLightStream CloneStream(int bodySize)
		{
			return new MemoryStream2(BufferUtil.Copy(this, bodySize));
		}

		internal void ConsumeTilEnd()
		{
			while (_currentFrameLenLeft != 0 || _remainingTotalBody != 0)
			{
				if (_currentFrameLenLeft != 0)
				{
					_innerStream.Seek(_currentFrameLenLeft, SeekOrigin.Current);

					_position += _currentFrameLenLeft;

					_currentFrameLenLeft = 0;

					if (_remainingTotalBody != 0)
					{
						ParseFrame(); // updates _currentFrameLenLeft and _remainingTotalBody
					}
				}
			}
		}

		internal void ReadAllInto(byte[] buffer, int offset, int bodySize)
		{
			var totalRead = 0;
			while (bodySize - totalRead != 0)
			{
				var read = this.Read(buffer, offset + totalRead, bodySize - totalRead);
				if (read == 0) break;
				totalRead += read;
			}
		}

		// Note: *not* consuming the final FrameEnd is the correct behavior. 
		public override int Read(byte[] buffer, int offset, int count)
		{
			int toRead;

			tryAgain: // label/goto vs fake while loop...
			{
				toRead = Math.Min(_currentFrameLenLeft, count);

				if (toRead == 0)
				{
					if (_remainingTotalBody == 0)
					{
						return 0; // EOF
					}

					ParseFrame(); // updates _currentFrameLenLeft and _remainingTotalBody

					goto tryAgain;
				}
			}

			var read = _innerStream.Read(buffer, offset, toRead, fillBuffer: true);

			_position += read;
			_currentFrameLenLeft -= read;

			return read;
		}

		public override long Length
		{
			get { return _remainingTotalBody + _currentFrameLenLeft; }
		}

		public override long Position
		{
			get { return _position; }
			set { throw new NotSupportedException(); }
		}

		private void ParseFrame()
		{
			Array.Clear(_frameBuffer, 0, _frameBuffer.Length);

			// Consume next Frame_End + start of next body
			// end (1) + start (1) + channel (2) + len (4)
			_innerStream.Read(_frameBuffer, 0, _frameBuffer.Length, fillBuffer: true);

			// Sanity check
			if (_frameBuffer[0] != AmqpConstants.FrameEnd) throw new Exception("Expecting frame end");
			if (_frameBuffer[1] != AmqpConstants.FrameBody) throw new Exception("Expecting frame body start");
			// ignoring the channel

			// next size
			Array.Reverse(_frameBuffer, 4, 4);
			var nextFrameLength = BitConverter.ToUInt32(_frameBuffer, 4);
			_currentFrameLenLeft = (int)nextFrameLength;
			if (_currentFrameLenLeft <= 0) throw new Exception("Unexpected size: " + _currentFrameLenLeft);
			_remainingTotalBody -= _currentFrameLenLeft;
		}
	}
}