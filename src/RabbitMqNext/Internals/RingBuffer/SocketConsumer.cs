namespace RabbitMqNext.Internals.RingBuffer
{
	using System;
	using System.Net.Sockets;
	using System.Threading;


	/// <summary>
	/// Gets the available content from 
	/// the ringbuffer and writes into the socket
	/// </summary>
	/// <remarks>
	/// Socket must be ready to be consumed
	/// </remarks>
	internal class SocketConsumer
	{
		private readonly Socket _socket;
		private readonly ByteRingBuffer _ringBuffer;
		private readonly CancellationToken _cancellationToken;

		public SocketConsumer(Socket socket, ByteRingBuffer ringBuffer,
							  CancellationToken cancellationToken, int index)
		{
			_socket = socket;
			_ringBuffer = ringBuffer;
			_cancellationToken = cancellationToken;

			ThreadFactory.CreateBackgroundThread(WriteSocketFromRingBuffer, "SocketConsumer_" + index);
		}

		public event Action<Socket, Exception> OnNotifyClosed;

		private void WriteSocketFromRingBuffer(object obj)
		{
			try
			{
				while (!_cancellationToken.IsCancellationRequested)
				{
					_ringBuffer.ReadBufferIntoSocketSend(_socket);
				}
			}
			catch (SocketException ex)
			{
				Console.WriteLine("SocketConsumer Socket Error " + ex);
				FireClosed(ex);
//				throw;
			}
			catch (Exception ex)
			{
				Console.WriteLine("SocketConsumer Error " + ex);
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