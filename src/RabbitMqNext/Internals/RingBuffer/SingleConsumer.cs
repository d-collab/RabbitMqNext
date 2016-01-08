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
			return _ringBuffer.Read(buffer, offset, count, fillBuffer);

//			int totalRead = 0;
//			while (!_cancellationToken.IsCancellationRequested)
//			{
//				var claimedSize = _ringBuffer.ClaimReadRegion(waitIfNothingAvailable: fillBuffer, desiredCount: count);
//
//				if (claimedSize == 0) // something's wrong if fillBuffer == true
//				{
//					return 0;
//				}
//
//				var lenToRead = Math.Min(count - totalRead, claimedSize);
//
//				_ringBuffer.Read(buffer, offset + totalRead, lenToRead);
//
//				totalRead += lenToRead;
//
//				if (fillBuffer && totalRead < count) 
//					continue;
//
//				if (!fillBuffer || totalRead == count) break;
//			}
//
//			return totalRead;
		}

		public Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
		{
			return _ringBuffer.ReadAsync(buffer, offset, count, true, cancellationToken);

//			while (!_cancellationToken.IsCancellationRequested)
//			{
//				var claimedSize = await _ringBuffer.ClaimReadRegionAsync(count);
//
//				if (claimedSize == 0) // something's wrong if fillBuffer == true
//				{
//					return 0;
//				}
//
//				var lenToRead = Math.Min(count, claimedSize);
//
//				_ringBuffer.Read(buffer, offset, lenToRead);
//
//				return lenToRead;
//			}
//
//			return 0;
		}

		public int Skip(long offset)
		{
			checked
			{
				var iOffset = (int) offset;
				return _ringBuffer.Skip(iOffset);
//				var claimedSize = _ringBuffer.ClaimReadRegion(iOffset, waitIfNothingAvailable: false);
//				if (claimedSize > 0)
//				{
//					_ringBuffer.CommitRead(claimedSize); // commit as if we consumed the buffer
//				}
//				return claimedSize;
			}
		}
	}
}