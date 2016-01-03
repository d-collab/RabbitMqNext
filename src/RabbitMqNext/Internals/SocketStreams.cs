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

//		private const int BufferSize = 1024 * 128;
//		private ArrayPool<byte> _bufferPool = new DefaultArrayPool<byte>(BufferSize, 100);

		private volatile int _socketIsClosed = 0;

//		private AsyncManualResetEvent _asyncManualReset = new AsyncManualResetEvent(false);

		public SocketStreams(Socket socket, CancellationToken token, Action notifyWhenClosed)
		{
			_socket = socket;
			_token = token;
			_notifyWhenClosed = notifyWhenClosed;

			_ringBufferStream = new RingBufferStream(token);

			_outputRingBuffer = new RingBufferStream(token);

			Task.Factory.StartNew((_) => ReadLoop(null), token, TaskCreationOptions.LongRunning);
			Task.Factory.StartNew((_) => WriteLoop(null), token, TaskCreationOptions.LongRunning);

//			var tempBuffer = new byte[BufferSize];

			Writer = new InternalBigEndianWriter(_outputRingBuffer);

//			Writer = new InternalBigEndianWriter(  (buffer, off, count) =>
//			{
//				// async write to socket
//				// await _socket.WriteAsync(buffer, off, count);
//				// Console.WriteLine("About to write to socket (Len) " + count);
//				
//				if (_socketIsClosed == 1)
//				{
//					Console.WriteLine("Ignoring socket write since looks like it's closed. Connected? " + _socket.Connected);
//					return;
//				}
//
//				try
//				{
////					if (count <= 8)
//					{
//						_socket.WriteSync(buffer, off, count);
//					}
////					else
////					{
//						// var temp = _bufferPool.Rent(BufferSize);
////						Buffer.BlockCopy(buffer, off, tempBuffer, 0, count);
////
////						_socket.WriteAsync(tempBuffer, off, count);
////
////						using (var ev = new SocketAsyncEventArgs())
////						{
////							ev.SetBuffer(tempBuffer, 0, count);
////
////							// ev.Completed += (sender, args) =>
////							{
////								// _asyncManualReset.Set2();
//////								_bufferPool.Return(tempBuffer);
////
////								// Assert count == args.BytesTransferred
////							};
////
////							if (!_socket.SendAsync(ev))
////							{
//////								_bufferPool.Return(tempBuffer);
////								// await _asyncManualReset.WaitAsync(_token);
////
////								// Assert count == args.BytesTransferred
////							}
////						}
////					}
//
//					// _socket.WriteSync(buffer, off, count);
////					await _socket.WriteAsync(buffer, off, count);
//				}
//				catch (SocketException e)
//				{
//					_socketIsClosed = 1;
//					_notifyWhenClosed();
//					Console.WriteLine("[Error] 1 - " + e.Message);
////					throw;
//				}
//				catch (Exception e)
//				{
//					Console.WriteLine("[Error] 2 - " + e.Message);
//				}
//
//			});

			Reader = new InternalBigEndianReader(_ringBufferStream);
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
				_socketIsClosed = 1;
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
			var buffer = new byte[2048];

			while (!_token.IsCancellationRequested && _socketIsClosed == 0)
			{
				int read = 0;

				try
				{
//					using (var ev = new SocketAsyncEventArgs())
//					{
//						ev.SetBuffer(buffer, 0, buffer.Length);
//						ev.Completed += (sender, args) =>
//						{
//							_ringBufferStream.Insert(args.Buffer, args.Offset, args.BytesTransferred);
//							locker.Set();
//						};
//
//						if (!_socket.ReceiveAsync(ev))
//						{
//							_ringBufferStream.Insert(ev.Buffer, ev.Offset, ev.BytesTransferred);
//							continue;
//						}
//						else
//						{
//							// opStarted = true;
//							locker.Wait(_token);
//							continue;
//						}
//					}

					// read = _socket.Receive(buffer, 0, buffer.Length, SocketFlags.None);
//					read = await _socket.ReceiveTaskAsync(buffer, 0, buffer.Length);

					await _ringBufferStream.ReceiveFromTask(_socket);
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
				
//				if (read != 0)
				{
					// this call may lock
					// _ringBufferStream.Insert(buffer, 0, read);
				}	
			}
		}

		public void Dispose()
		{
			_ringBufferStream.Dispose();
		}

	}
}