namespace RabbitMqNext.Internals.RingBuffer
{
	using System;
	using System.Threading;
	

	internal class SingleConsumer
	{
		private readonly RingBufferStream2 _ringBuffer;
		private readonly CancellationToken _cancellationToken;

		public SingleConsumer(RingBufferStream2 ringBuffer, CancellationToken cancellationToken)
		{
			_ringBuffer = ringBuffer;
			_cancellationToken = cancellationToken;
		}

		public int Read(byte[] buffer, int offset, int count, bool fillBuffer = false)
		{
			int totalRead = 0;
			while (!_cancellationToken.IsCancellationRequested)
			{
				var claimedSize = _ringBuffer.ClaimReadRegion(); // may block

				if (claimedSize == 0)
				{
					throw new Exception("wtf2");
				}

				var lenToRead = Math.Min(count - totalRead, claimedSize);

				_ringBuffer.ReadClaimedRegion(buffer, offset + totalRead, lenToRead);

				totalRead += lenToRead;

				if (fillBuffer && totalRead < count) 
					continue;

				if (!fillBuffer || totalRead == count) break;
			}

			return totalRead;
		}
	}
}