﻿namespace RabbitMqNext.Internals.RingBuffer
{
	using System;
	using System.Threading;
	using System.Threading.Tasks;


	internal class SingleProducer
	{
		private readonly RingBuffer2 _ringBuffer;
		private readonly CancellationToken _cancellationToken;

		public SingleProducer(RingBuffer2 ringBuffer, CancellationToken cancellationToken)
		{
			_ringBuffer = ringBuffer;
			_cancellationToken = cancellationToken;
		}

		public void Write(byte[] buffer, int offset, int count)
		{
			_ringBuffer.Write(buffer, offset, count, writeAll: true);

//			var written = 0;
//			while (written < count && !_cancellationToken.IsCancellationRequested)
//			{
//				var claimSize = count - written;
//				var available = _ringBuffer.ClaimWriteRegion(claimSize); // may block
//
//				if (available == 0) 
//				{
//					throw new Exception("wtf1");
//				}
//
//				_ringBuffer.Write(buffer, offset + written, available);
//
//				written += available;
//			}
		}

		public Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
		{
			return _ringBuffer.WriteAsync(buffer, offset, count, true, cancellationToken);

//			var written = 0;
//			while (written < count && !_cancellationToken.IsCancellationRequested)
//			{
//				var claimSize = count - written;
//				var available = await _ringBuffer.ClaimWriteRegionAsync(claimSize); // may block
//
//				if (available == 0)
//				{
//					throw new Exception("wtf1");
//				}
//
//				_ringBuffer.Write(buffer, offset + written, available);
//
//				written += available;
//			}
		}
	}
}