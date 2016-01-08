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
		private readonly RingBuffer2 _ringBuffer;
		private readonly CancellationToken _cancellationToken;
		private readonly bool _asyncSend;

		public SocketConsumer(Socket socket, RingBuffer2 ringBuffer, 
							  CancellationToken cancellationToken, 
							  bool asyncSend = false)
		{
			_socket = socket;
			_ringBuffer = ringBuffer;
			_cancellationToken = cancellationToken;
			_asyncSend = asyncSend;
			// Task.Factory.StartNew(WriteSocketFromRingBuffer, cancellationToken, TaskCreationOptions.LongRunning);

			ThreadFactory.CreateBackgroundThread(WriteSocketFromRingBuffer, "SocketConsumer");
		}

		public event Action<Socket, Exception> OnNotifyClosed;

		private async void WriteSocketFromRingBuffer(object obj)
		{
			try
			{
				while (!_cancellationToken.IsCancellationRequested)
				{
					var available = _ringBuffer.ClaimReadRegion(waitIfNothingAvailable: true);

					if (available == 0) throw new Exception("wtf2");

					await _ringBuffer.ReadClaimedRegion(_socket, available, _asyncSend);
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