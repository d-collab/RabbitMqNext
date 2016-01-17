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
				Console.WriteLine("SocketProducer Socket Error " + ex);
				FireClosed(ex);
//				throw;
			}
			catch (Exception ex)
			{
				Console.WriteLine("SocketProducer Error " + ex);
				FireClosed(ex);
//				throw;
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