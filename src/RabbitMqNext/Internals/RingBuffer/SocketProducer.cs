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
		private readonly RingBuffer2 _ringBuffer;
		private readonly CancellationToken _cancellationToken;
		private readonly bool _asyncRecv;

		public SocketProducer(Socket socket, RingBuffer2 ringBuffer, 
							  CancellationToken cancellationToken, bool asyncRecv = false)
		{
			_socket = socket;
			_ringBuffer = ringBuffer;
			_cancellationToken = cancellationToken;
			_asyncRecv = asyncRecv;
			// Task.Factory.StartNew(ReadSocketIntoRingBuffer, cancellationToken, TaskCreationOptions.LongRunning);
			ThreadFactory.CreateBackgroundThread(ReadSocketIntoRingBuffer, "SocketProducer");
		}

		public event Action<Socket, Exception> OnNotifyClosed;

		private async void ReadSocketIntoRingBuffer(object obj)
		{
			try
			{
				while (!_cancellationToken.IsCancellationRequested)
				{
//					var available = _ringBuffer.ClaimWriteRegion(); // may block
//					if (available == 0) throw new Exception("wtf1");
//					await _ringBuffer.WriteToClaimedRegionFrom(_socket, available, asyncRecv: _asyncRecv);

					await _ringBuffer.WriteBufferFromSocketRecv(_socket, asyncRecv: _asyncRecv);
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