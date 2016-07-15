namespace RabbitMqNext.Internals
{
	using System;
	using System.Buffers;
	using System.IO;
	using System.Threading;
	using System.Threading.Tasks;

	internal class MemoryStreamSlim : Stream
	{
		private readonly ArrayPool<byte> _pool;
		private readonly int _arraySize;
		private byte[] _buffer;
		private int _position;

		public MemoryStreamSlim(ArrayPool<byte> pool, int arrayMinSize)
		{
			_pool = pool;
			_arraySize = arrayMinSize;
		}

		public byte[] InternalBuffer { get { return _buffer; } }

		protected override void Dispose(bool disposing)
		{
			_position = 0;
			if (_buffer != null)
			{
				_pool.Return(_buffer);
				_buffer = null;
			}
		}

		public override long Seek(long offset, SeekOrigin origin)
		{
			throw new NotSupportedException();
		}

		public override void SetLength(long value)
		{
			throw new NotSupportedException();
		}

		public override int Read(byte[] buffer, int offset, int count)
		{
			return 0;
		}

		public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
		{
			Write(buffer, offset, count);
			return Task.CompletedTask;
		}

		public override void Write(byte[] buffer, int offset, int count)
		{
			if (_buffer == null)
			{
				_buffer = _pool.Rent(_arraySize);
			}
			BufferUtil.FastCopy(_buffer, _position, buffer, offset, count);
//			if (count <= 8)
//			{
//				int byteCount = count;
//				while (--byteCount >= 0)
//					_buffer[_position + byteCount] = buffer[offset + byteCount];
//			}
//			else
//			{
//				// TODO: better/faster option?
//				Buffer.BlockCopy(buffer, offset, _buffer, _position, count);	
//			}
			_position += count;
		}

		public override bool CanRead
		{
			get { return false; }
		}

		public override bool CanSeek
		{
			get { return false; }
		}

		public override bool CanWrite
		{
			get { return true; }
		}

		public override long Length
		{
			get { return _arraySize; }
		}

		public override void Flush()
		{
		}

		public override long Position
		{
			get
			{
				return _position;
			}
			set
			{
				
			}
		}
	}
}