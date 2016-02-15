namespace RabbitMqNext.Internals.RingBuffer
{
	using System;
	using System.IO;
	using System.Net.Sockets;
	using System.Threading;

	internal class SocketStreamWriterAdapter : Stream
	{
		private readonly Socket _socket;
		private readonly CancellationToken _cancellationToken;

		public SocketStreamWriterAdapter(Socket socket, CancellationToken cancellationToken)
		{
			_socket = socket;
			_cancellationToken = cancellationToken;
		}

		public event Action<Socket, Exception> OnNotifyClosed;

		public override void Write(byte[] buffer, int offset, int count)
		{
			try
			{
				var totalSent = 0;
				while (totalSent < count)
				{
					var sent = _socket.Send(buffer, totalSent, count - totalSent, SocketFlags.None);
					totalSent += sent;
				}
			}
			catch (SocketException ex)
			{
				Console.WriteLine("SocketConsumer Socket Error " + ex);
				FireClosed(ex);
				// throw;
			}
			catch (Exception ex)
			{
				Console.WriteLine("SocketConsumer Error " + ex);
				FireClosed(ex);
				// throw;
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