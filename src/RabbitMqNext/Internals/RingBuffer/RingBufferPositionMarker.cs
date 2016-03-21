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
		private uint _start;

		public RingBufferPositionMarker(RingBufferStreamAdapter ringBuffer)
		{
			if (ringBuffer != null)
			{
				_ringBuffer = ringBuffer._ringBuffer;
				_start = _ringBuffer.GlobalReadPos;
			}
			else
			{
				_ringBuffer = null;
				_start = 0;
			}
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
	}
}