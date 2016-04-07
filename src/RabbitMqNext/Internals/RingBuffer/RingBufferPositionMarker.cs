namespace RabbitMqNext.Internals.RingBuffer
{
	/// <summary>
	/// Measures how much has been read since started, 
	/// limited to small amounts (wont support overflowing over and over)
	/// </summary>
	/// TODO: broken when used with ReadGates, needs fix
	internal struct RingBufferPositionMarker
	{
		private readonly ByteRingBuffer _ringBuffer;
		private readonly MultiBodyStreamWrapper _mbStreamWrapper;
		private uint _start;

		public RingBufferPositionMarker(BaseLightStream untypedStream)
		{
			_ringBuffer = null;
			_mbStreamWrapper = untypedStream as MultiBodyStreamWrapper;
			_start = 0;
			var adapter = untypedStream as RingBufferStreamAdapter;
			if (adapter != null)
			{
				_ringBuffer = adapter._ringBuffer;
				_start = _ringBuffer.GlobalReadPos;
			}
		}

		public RingBufferPositionMarker(RingBufferStreamAdapter ringBuffer)
		{
			_mbStreamWrapper = null;
			_ringBuffer = ringBuffer._ringBuffer;
			_start = _ringBuffer.GlobalReadPos;
		}

		public uint LengthRead
		{
			get
			{
				if (_ringBuffer == null) return 0;

				var curReadPos = _ringBuffer.GlobalReadPos;
				if (curReadPos < _start) // overflowed
				{
					return (uint.MaxValue - _start) + curReadPos;
				}
				return curReadPos - _start;
			}
		}

		public void EnsureConsumed(int bodySize)
		{
			if (_mbStreamWrapper != null)
			{
				_mbStreamWrapper.ConsumeTilEnd();
				return;
			}

			if (_ringBuffer != null)
			{
				var totalRead = this.LengthRead;
				if (totalRead < bodySize) // if less than available was used, move stream fwd
				{
					int offset = checked( (int)(bodySize - totalRead) );
					_ringBuffer.Skip(offset);
				}	
			}
		}
	}
}