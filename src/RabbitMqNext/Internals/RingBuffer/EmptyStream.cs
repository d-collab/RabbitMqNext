namespace RabbitMqNext.Internals
{
	using System;
	using System.IO;

	public sealed class EmptyStream : Stream
	{
		public override long Seek(long offset, SeekOrigin origin)
		{
			return 0;
		}

		public override void SetLength(long value)
		{
			throw new NotImplementedException();
		}
		
		public override bool CanRead
		{
			get { return true; }
		}

		public override int Read(byte[] buffer, int offset, int count)
		{
			return 0;
		}

		public override void Write(byte[] buffer, int offset, int count)
		{
			throw new NotImplementedException();
		}

		public override void Flush()
		{
		}

		public override long Length
		{
			get { return 0; }
		}

		public override long Position { get; set; }

		public override bool CanSeek
		{
			get { return false; }
		}

		public override bool CanWrite
		{
			get { return false; }
		}

		protected override void Dispose(bool disposing)
		{
			// no action
		}
	}
}