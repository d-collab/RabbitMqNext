namespace RabbitMqNext.Internals
{
	using System;
	using System.Net.Sockets;
	using System.Threading;
	using System.Threading.Tasks;

	internal class SocketStreams
	{
		private readonly Socket _socket;
		private readonly CancellationToken _token; // does not own it
		private readonly RingBufferStream _ringBufferStream = new RingBufferStream();
		private readonly Action _notifyWhenClosed;

		internal readonly InternalBigEndianWriter Writer;
		internal readonly InternalBigEndianReader Reader;

		private volatile int _socketIsClosed = 0;

		public SocketStreams(Socket socket, CancellationToken token, Action notifyWhenClosed)
		{
			_socket = socket;
			_token = token;
			_notifyWhenClosed = notifyWhenClosed;

			Task.Factory.StartNew(ReadLoop, token, TaskCreationOptions.LongRunning);

			Writer = new InternalBigEndianWriter((buffer, off, count) =>
			{
				// async write to socket
				// await _socket.WriteAsync(buffer, off, count);

				// Console.WriteLine("About to write to socket (Len) " + count);

				if (_socketIsClosed == 1)
				{
					Console.WriteLine("Ignoring socket write since looks like it's closed. Connected? " + _socket.Connected);
					return;
				}

				try
				{
					_socket.WriteSync(buffer, off, count);
				}
				catch (SocketException e)
				{
					_socketIsClosed = 1;
					_notifyWhenClosed();
					Console.WriteLine("[Error] 1 - " + e.Message);
//					throw;
				}
				catch (Exception e)
				{
					Console.WriteLine("[Error] 2 - " + e.Message);
				}

			});

			Reader = new InternalBigEndianReader(_ringBufferStream);
		}

		private async void ReadLoop(object state)
		{
			var buffer = new byte[2048];

			while (!_token.IsCancellationRequested && _socketIsClosed == 0)
			{
				int read = 0;

				try
				{
					read = await _socket.ReceiveTaskAsync(buffer, 0, buffer.Length);
				}
				catch (SocketException ex)
				{
					_socketIsClosed = 1;
					_notifyWhenClosed();
					Console.WriteLine("[Error] 3 - " + ex.Message);
//					throw;
				}
				catch (Exception ex)
				{
					Console.WriteLine("[Error] 4 - " + ex.Message);
					// throw;
				}
				
				if (read != 0)
				{
					// this call may lock
					_ringBufferStream.Insert(buffer, 0, read);
				}	
			}
		}
	}
}