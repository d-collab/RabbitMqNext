namespace RabbitMqNext.Internals.RingBuffer
{
	using System;
	using System.IO;
	using System.Net.Sockets;
	using System.Threading;

	internal class SocketStreamWriterAdapter : Stream
	{
		private readonly Socket _socket;

		public SocketStreamWriterAdapter(Socket socket)
		{
			_socket = socket;
		}

		public event Action<Socket, Exception> OnNotifyClosed;

		public override void Write(byte[] buffer, int offset, int count)
		{
			try
			{
				var totalSent = 0;
				while (totalSent < count)
				{
					var sent = _socket.Send(buffer, offset + totalSent, count - totalSent, SocketFlags.None);
					totalSent += sent;
				}
			}
			catch (SocketException ex)
			{
				if (LogAdapter.ExtendedLogEnabled)
					LogAdapter.LogError("SocketStreamWriterAdapter", "Socket error", ex);

				FireClosed(ex);
			}
			catch (Exception ex)
			{
				if (LogAdapter.ExtendedLogEnabled)
					LogAdapter.LogError("SocketStreamWriterAdapter", "Error", ex);

				FireClosed(ex);
			}
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
			get { return 0; }
		}

		public override long Position { get; set; }

		public override void Flush()
		{
		}

		public override long Seek(long offset, SeekOrigin origin)
		{
			throw new NotImplementedException();
		}

		public override void SetLength(long value)
		{
			throw new NotImplementedException();
		}

		public override int Read(byte[] buffer, int offset, int count)
		{
			throw new NotImplementedException();
		}

		private void FireClosed(Exception exception)
		{
			var ev = this.OnNotifyClosed;
			if (ev != null)
			{
				ev(_socket, exception);
			}
		}
	}
}