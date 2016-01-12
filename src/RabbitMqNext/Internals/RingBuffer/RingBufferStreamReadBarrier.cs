namespace RabbitMqNext.Internals.RingBuffer
{
	using System;
	using System.IO;
	using System.Threading;
	using System.Threading.Tasks;

	public class RingBufferStreamReadBarrier : Stream
	{
//		private readonly RingBufferStreamAdapter _innerStream;
		private readonly ReadingGate _gate;
		private readonly ByteRingBuffer _ringBuffer;
		private volatile bool _released = false;

		public RingBufferStreamReadBarrier(RingBufferStreamAdapter innerStream, int length)
		{
			_ringBuffer = innerStream._ringBuffer;
			// _length = length;
			_gate = _ringBuffer.AddReadingGate((uint)length);
		}

		public void Release()
		{
			if (_released) return;
			_released = true;

			_ringBuffer.RemoveReadingGate(_gate);
		}

		protected override void Dispose(bool disposing)
		{
			this.Release();
			base.Dispose(disposing);
		}

		public override int Read(byte[] buffer, int offset, int count)
		{
			var lenToRead = Math.Min(count, (int)_gate.length);
			if (lenToRead == 0) return 0; // user cannot read ahead of its window into the real buffer
			var read = _ringBuffer.Read(buffer, offset, lenToRead, fillBuffer: true, fromGate: _gate);
			return read;
		}

		public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
		{
			throw new NotImplementedException();
//			return base.ReadAsync(buffer, offset, count, cancellationToken);
		}

		public override void Flush()
		{
			throw new NotImplementedException();
		}

		public override long Seek(long offset, SeekOrigin origin)
		{
			throw new NotImplementedException();
		}

		public override void SetLength(long value)
		{
			throw new NotImplementedException();
		}

		public override void Write(byte[] buffer, int offset, int count)
		{
			throw new NotImplementedException();
		}

		public override bool CanRead
		{
			get { return true; }
		}

		public override bool CanSeek
		{
			get { return false; }
		}

		public override bool CanWrite
		{
			get { return false; }
		}

		public override long Length
		{
			get { throw new NotImplementedException(); ; }
		}

		public override long Position
		{
			get { throw new NotImplementedException(); }
			set { throw new NotImplementedException(); }
		}
	}
}