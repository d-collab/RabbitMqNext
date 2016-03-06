namespace RabbitMqNext.Internals.RingBuffer
{
	using System;
	using System.Net.Sockets;
	using System.Threading;

	/// <summary>
	/// Writes to ringbuffer content from socket
	/// </summary>
	/// <remarks>
	/// Socket must be ready to be consumed
	/// </remarks>
	internal class SocketProducer
	{
		private readonly Socket _socket;
		private readonly ByteRingBuffer _ringBuffer;
		private readonly CancellationToken _cancellationToken;

		public SocketProducer(Socket socket, ByteRingBuffer ringBuffer, 
							  CancellationToken cancellationToken, int index)
		{
			_socket = socket;
			_ringBuffer = ringBuffer;
			_cancellationToken = cancellationToken;

			ThreadFactory.CreateBackgroundThread(ReadSocketIntoRingBuffer, "SocketProducer_" + index);
		}

		public event Action<Socket, Exception> OnNotifyClosed;

		private void ReadSocketIntoRingBuffer(object obj)
		{
			try
			{
				while (!_cancellationToken.IsCancellationRequested)
				{
					_ringBuffer.WriteBufferFromSocketRecv(_socket);
				}
			}
			catch (SocketException ex)
			{
				if (LogAdapter.ExtendedLogEnabled)
					LogAdapter.LogError("SocketProducer", "Socket error", ex);

				FireClosed(ex);
			}
			catch (Exception ex)
			{
				LogAdapter.LogError("SocketProducer", "Error", ex);

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