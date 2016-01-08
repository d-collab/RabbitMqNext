namespace RabbitMqNext.Internals.RingBuffer
{
	using System;
	using System.Threading;
	using System.Threading.Tasks;


	internal class SingleConsumer
	{
		private readonly RingBuffer2 _ringBuffer;
		private readonly CancellationToken _cancellationToken;

		public SingleConsumer(RingBuffer2 ringBuffer, CancellationToken cancellationToken)
		{
			_ringBuffer = ringBuffer;
			_cancellationToken = cancellationToken;
		}

		public int Read(byte[] buffer, int offset, int count, bool fillBuffer = false)
		{
			int totalRead = 0;
			while (!_cancellationToken.IsCancellationRequested)
			{
				var claimedSize = _ringBuffer.ClaimReadRegion(waitIfNothingAvailable: fillBuffer, desiredCount: count);

				if (claimedSize == 0) // something's wrong if fillBuffer == true
				{
					return 0;
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

		public async Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
		{
			while (!_cancellationToken.IsCancellationRequested)
			{
				var claimedSize = await _ringBuffer.ClaimReadRegionAsync(count);

				if (claimedSize == 0) // something's wrong if fillBuffer == true
				{
					return 0;
				}

				var lenToRead = Math.Min(count, claimedSize);

				_ringBuffer.ReadClaimedRegion(buffer, offset, lenToRead);

				return lenToRead;
			}

			return 0;
		}

		public int Skip(long offset)
		{
			checked
			{
				var iOffset = (int) offset;
				var claimedSize = _ringBuffer.ClaimReadRegion(iOffset, waitIfNothingAvailable: false);
				if (claimedSize > 0)
				{
					_ringBuffer.CommitRead(claimedSize); // commit as if we consumed the buffer
				}
				return claimedSize;
			}
		}
	}
}