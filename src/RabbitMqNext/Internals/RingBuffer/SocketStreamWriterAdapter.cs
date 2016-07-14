namespace RabbitMqNext.Internals.RingBuffer
{
	using System;
	using System.IO;
	using System.Net.Sockets;


	internal class SocketStreamWriterAdapter //: Stream
	{
		private readonly Socket _socket;

		public SocketStreamWriterAdapter(Socket socket)
		{
			_socket = socket;
		}

		public Action<Socket, Exception> OnNotifyClosed;

		public void Write(byte[] buffer, int offset, int count)
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