namespace RabbitMqNext.Internals
{
	using System;


	/// <summary>
	/// Same as MemoryStream, but can surrender the internal buffer
	/// </summary>
	public class MemoryStream2 : BaseLightStream
	{
		private readonly byte[] _buffer;
		private int _position;
		private bool _disposed;

		public MemoryStream2(byte[] buffer)
		{
			if (buffer == null) throw new ArgumentNullException("buffer");

			_buffer = buffer;
		}

		public override BaseLightStream CloneStream(int bodySize)
		{
			throw new NotSupportedException();
		}

		public byte[] InnerBuffer { get { return _buffer; } }

		protected override void Dispose(bool disposing)
		{
			_disposed = true;

			base.Dispose(disposing);
		}

		public override int Read(byte[] buffer, int offset, int count)
		{
			if (_disposed) throw new ObjectDisposedException("MemoryStream2");

			int toCopy = _buffer.Length - _position;
			if (toCopy > count) toCopy = count;
			if (toCopy <= 0) return 0;

			BufferUtil.FastCopy(buffer, offset, _buffer, _position, toCopy);

//			if (toCopy <= 8)
//			{
//				int bCount = toCopy;
//				while (--bCount >= 0)
//				{
//					buffer[offset + bCount] = _buffer[_position + bCount];
//				}
//			}
//			else
//			{
//				Buffer.BlockCopy(_buffer, _position, buffer, offset, toCopy);
//			}
			_position += toCopy;

			return toCopy;
		}

		public override long Length
		{
			get { return _buffer.Length - _position; }
		}

		public override long Position
		{
			get { return _position; }
			set { throw new NotSupportedException(); }
		}
	}
}