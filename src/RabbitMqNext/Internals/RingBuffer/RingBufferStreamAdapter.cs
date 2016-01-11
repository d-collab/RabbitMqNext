namespace RabbitMqNext.Internals.RingBuffer
{
	using System;
	using System.IO;
	using System.Threading;
	using System.Threading.Tasks;

	public class RingBufferStreamAdapter : Stream
	{
//		private readonly SingleConsumer _consumer;
//		private readonly SingleProducer _producer;
		internal readonly BufferRingBuffer _ringBuffer;

		internal RingBufferStreamAdapter(BufferRingBuffer ringBuffer)
		{
			_ringBuffer = ringBuffer;
//			_consumer = new SingleConsumer(ringBuffer, ringBuffer.CancellationToken);
//			_producer = new SingleProducer(ringBuffer, ringBuffer.CancellationToken);
		}

		public CancellationToken CancellationToken
		{
			get { return _ringBuffer.CancellationToken; }
		}

		public override int Read(byte[] buffer, int offset, int count)
		{
			// Note: fills up the buffer or hangs
			// return _consumer.Read(buffer, offset, count, fillBuffer: true);
			return _ringBuffer.Read(buffer, offset, count, fillBuffer: true);
		}
		
		public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
		{
//			 return _consumer.ReadAsync(buffer, offset, count, cancellationToken);
//			return _ringBuffer.ReadAsync(buffer, offset, count, fillBuffer: true, cancellationToken: cancellationToken);
			throw new NotImplementedException();
		}

		public override void Write(byte[] buffer, int offset, int count)
		{
			// Note: this writes all buffer content
			// _producer.Write(buffer, offset, count);
			_ringBuffer.Write(buffer, offset, count, writeAll: true);
		}

		public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
		{
			// return _producer.WriteAsync(buffer, offset, count, cancellationToken);
			return _ringBuffer.WriteAsync(buffer, offset, count, true, cancellationToken);
		}

		public override long Seek(long offset, SeekOrigin origin)
		{
			if (origin != SeekOrigin.Current) throw new NotSupportedException("Only from current is supported");

			// checked
			{
				var offsetAsInt = (int)offset;
				_ringBuffer.Skip(offsetAsInt);
				return offset;
			}
		}

		public override void SetLength(long value)
		{
			throw new NotSupportedException();
		}

		public override bool CanRead
		{
			get { return true; }
		}

		public override bool CanSeek
		{
			get { return true; }
		}

		public override bool CanWrite
		{
			get { return true; }
		}

		public override long Length
		{
			get { throw new NotSupportedException(); ; }
		}

		public override long Position
		{
			get { throw new NotSupportedException(); }
			set { throw new NotSupportedException(); }
		}

		public override void Flush()
		{
		}
	}
}