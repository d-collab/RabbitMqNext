namespace RabbitMqNext.Internals
{
	using System;
	using System.Net.Sockets;
	using System.Threading;
	using RingBuffer;

	public class SocketRingBuffers : IDisposable
	{
		internal readonly RingBufferStreamAdapter _inputRingBufferStream;
		internal readonly RingBufferStreamAdapter _outputRingBufferStream;

		public readonly InternalBigEndianWriter Writer;
		public readonly InternalBigEndianReader Reader;

		private readonly RingBuffer2 _inputBuffer, _outputBuffer;
		private readonly SocketConsumer _socketConsumer;
		private readonly SocketProducer _socketProducer;

		private readonly Action _notifyWhenClosed;

		private int _socketIsClosed = 0;

		public SocketRingBuffers(Socket socket, CancellationToken cancellationToken, Action notifyWhenClosed)
		{
			_notifyWhenClosed = notifyWhenClosed;

			_inputBuffer = new RingBuffer2(cancellationToken);
			_outputBuffer = new RingBuffer2(cancellationToken);

			_inputRingBufferStream = new RingBufferStreamAdapter(_inputBuffer);
			_outputRingBufferStream = new RingBufferStreamAdapter(_outputBuffer);

			// WriteLoop
			_socketConsumer = new SocketConsumer(socket, _outputBuffer, cancellationToken);
			_socketConsumer.OnNotifyClosed += OnSocketClosed;

			// ReadLoop
			_socketProducer = new SocketProducer(socket, _inputBuffer, cancellationToken);
			_socketProducer.OnNotifyClosed += OnSocketClosed;

			Writer = new InternalBigEndianWriter(_outputRingBufferStream);
			Reader = new InternalBigEndianReader(_inputRingBufferStream);
		}

		private void OnSocketClosed(Socket arg1, Exception arg2)
		{
			if (Interlocked.CompareExchange(ref _socketIsClosed, 1, 0) == 0)
			{
				this._notifyWhenClosed();
			}
		}

		public bool StillSending
		{
			// get { return _outputRingBuffer.Position < _outputRingBuffer.Length; }
			get { return _outputBuffer.HasUnreadContent; }
		}

//		private async void WriteLoop()
//		{
//			try
//			{
//				while (!_cancellationToken.IsCancellationRequested && _socketIsClosed == 0)
//				{
//					// No intermediary buffer needed
//					await _outputRingBuffer.ReadAvailableBufferIntoSocketAsync(_socket);
//				}
//			}
//			catch (SocketException ex)
//			{
//				Interlocked.Increment(ref _socketIsClosed);
//				_notifyWhenClosed();
//				Console.WriteLine("[Error] 5 - " + ex.Message);
//				// throw;
//			}
//			catch (Exception ex)
//			{
//				Console.WriteLine("[Error] 6 - " + ex.Message);
//				// throw;
//			}
//		}

//		private async void ReadLoop()
//		{
//			while (!_cancellationToken.IsCancellationRequested && _socketIsClosed == 0)
//			{
//				try
//				{
//					// will block
//					await _ringBufferStream.ReceiveFromTask(_socket);
//				}
//				catch (SocketException ex)
//				{
//					Interlocked.Increment(ref _socketIsClosed);
//					_notifyWhenClosed();
//					Console.WriteLine("[Error] 3 - " + ex.Message);
////					throw;
//				}
//				catch (Exception ex)
//				{
//					Console.WriteLine("[Error] 4 - " + ex.Message);
//					// throw;
//				}
//			}
//		}

		public void Dispose()
		{
			_inputRingBufferStream.Dispose();
			_outputRingBufferStream.Dispose();
		}
	}
}