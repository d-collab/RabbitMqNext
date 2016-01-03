namespace RabbitMqNext.Internals
{
	using System;
	using System.Buffers;
	using System.Net.Sockets;
	using System.Threading;
	using System.Threading.Tasks;

	internal class SocketStreams : IDisposable
	{
		private readonly Socket _socket;
		private readonly CancellationToken _token; // does not own it
		private readonly RingBufferStream _ringBufferStream;
		private readonly RingBufferStream _outputRingBuffer;
		private readonly Action _notifyWhenClosed;

		internal readonly InternalBigEndianWriter Writer;
		internal readonly InternalBigEndianReader Reader;

		private int _socketIsClosed = 0;

		public SocketStreams(Socket socket, CancellationToken token, Action notifyWhenClosed)
		{
			_socket = socket;
			_token = token;
			_notifyWhenClosed = notifyWhenClosed;

			_ringBufferStream = new RingBufferStream(token);

			_outputRingBuffer = new RingBufferStream(token);

			Task.Factory.StartNew((_) => ReadLoop(null), token, TaskCreationOptions.LongRunning);
			Task.Factory.StartNew((_) => WriteLoop(null), token, TaskCreationOptions.LongRunning);


			Writer = new InternalBigEndianWriter(_outputRingBuffer);

			Reader = new InternalBigEndianReader(_ringBufferStream);
		}

		public bool StillSending
		{
			get { return _outputRingBuffer.Position < _outputRingBuffer.Length; }
		}

		private async Task WriteLoop(object state)
		{
			try
			{
				while (!_token.IsCancellationRequested && _socketIsClosed == 0)
				{
					// No intermediary buffer needed
					await _outputRingBuffer.ReadIntoSocketTask(_socket, 18000);
				}
			}
			catch (SocketException ex)
			{
				Interlocked.Increment(ref _socketIsClosed);
				_notifyWhenClosed();
				Console.WriteLine("[Error] 5 - " + ex.Message);
				// throw;
			}
			catch (Exception ex)
			{
				Console.WriteLine("[Error] 6 - " + ex.Message);
				// throw;
			}
		}

		private async Task ReadLoop(object state)
		{
			while (!_token.IsCancellationRequested && _socketIsClosed == 0)
			{
				try
				{
					await _ringBufferStream.ReceiveFromTask(_socket);
				}
				catch (SocketException ex)
				{
					Interlocked.Increment(ref _socketIsClosed);
					_notifyWhenClosed();
					Console.WriteLine("[Error] 3 - " + ex.Message);
//					throw;
				}
				catch (Exception ex)
				{
					Console.WriteLine("[Error] 4 - " + ex.Message);
					// throw;
				}
			}
		}

		public void Dispose()
		{
			_ringBufferStream.Dispose();
		}
	}
}