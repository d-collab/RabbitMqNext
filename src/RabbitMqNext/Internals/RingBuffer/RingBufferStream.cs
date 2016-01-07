namespace RabbitMqNext.Internals
{
	using System;
	using System.IO;
	using System.Net.Sockets;
	using System.Runtime.CompilerServices;
	using System.Threading;
	using System.Threading.Tasks;

	

	/// <summary>
	/// This implementation presumes two things in order to work correctly:
	/// There are no concurrent reads and no concurrent writes. In other words,
	/// you can write from thread A and read from thread B, but you cannot have thread C
	/// writing, reading, seeking or you'll corrupt the positions. 
	/// 
	/// Yes, it could be made thread safe, but we are deliberately avoiding locks.
	/// </summary>
//	internal class RingBufferStream : Stream2
//	{
//		// must be power of 2 or the mod operation may produce wrong index
//		public const ulong BufferSize = 0x1A0000; // 1703936    // 1048576; // 0x100000
//
//		private readonly CancellationToken _cancellationToken;
//
//		private readonly ManualResetEventSlim _bufferFreeEvent = new ManualResetEventSlim(false);
//		private readonly ManualResetEventSlim _writeEvent = new ManualResetEventSlim(false, 100);
//		// private readonly ManualResetEventSlim _resetCursor = new ManualResetEventSlim(false);
//
//		private long _totalLen1 = 0L;
//		private long _readPosition1 = -1L;
//		private long _writePosition1 = -1L;
//
//		private byte[] _buffer;
//
//		public RingBufferStream(CancellationToken cancellationToken)
//		{
//			_cancellationToken = cancellationToken;
//			_buffer = new byte[BufferSize];
//		}
//
//		/// <summary>
//		/// append param buffer to our ring (single threaded producer)
//		/// </summary>
//		public Task Insert(byte[] buffer, int offset, int original)
//		{
//			if (original > BufferSize) throw new ArgumentException("overflowing the buffer? nope");
//
//			var writePosition = this.GetWriteCursorValue();
//
//
//
//			if (_writePosition == -1L)
//			{
//				_writePosition = 0L;
//			}
//
//			var totalCopied = 0;
//			var userBufferRemainLen = original;
//
//			while (!_cancellationToken.IsCancellationRequested)
//			{
//				// enough room?
//				if (BufferSize - UnreadLength() == 0) 
//				{
//					// Console.WriteLine("waiting");
//					{
//						// await _bufferFreeEvent.WaitAsync(_cancellationToken);
//						_bufferFreeEvent.Wait(_cancellationToken);
//						_bufferFreeEvent.Reset();
//						// await _bufferFreeEvent.WaitAsync(_cancellationToken);
//						continue;
//					}
//				}
//
//				var writePos = WriteCursorPosNormalized();
//
//				var sizeForCopy = Math.Min(BufferSize - writePos, userBufferRemainLen);
//				// Debug.Assert(sizeForCopy > 0, "will copy something");
//
//				if (BufferSize - writePos == 0)
//				{
//					// Console.WriteLine("waiting2");
//					{
//						_bufferFreeEvent.Wait(_cancellationToken);
//						_bufferFreeEvent.Reset();
//						// await _bufferFreeEvent.WaitAsync(_cancellationToken);
//						// await _bufferFreeEvent.WaitAsync(_cancellationToken);
//						continue;
//					}
//				}
//
//				var readpos = ReadCursorPosNormalized();
//				if (writePos < readpos && writePos + sizeForCopy > readpos)
//				{
//					// wrap!
//					// Debug.Assert(false, "will wrap");
//					sizeForCopy = readpos - writePos;
//				}
//
//				BlockCopy(buffer, offset, writePos, sizeForCopy);
//				
//				totalCopied += sizeForCopy; // total copied
//
//				// volatile writes
//				_totalLen += sizeForCopy;
//				_writePosition += sizeForCopy;
//
//				// signal
//				_writeEvent.Set();
//
//				if (totalCopied == original) break; // all copied?
//
//				offset += sizeForCopy; // offset of remainder
//				userBufferRemainLen -= sizeForCopy;
//			}
//
//			return Task.CompletedTask;
//		}
//
//		public Task ReadAvailableBufferIntoSocketAsync(Socket socket)
//		{
//			if (_readPosition == -1)
//			{
//				if (_writePosition != -1)
//					_readPosition = 0;
//				else
//					return Task.CompletedTask; // nothing was written yet
//			}
//
//			while (!_cancellationToken.IsCancellationRequested)
//			{
//				int unread = UnreadLength();
//				if (unread == 0)
//				{
////					await _writeEvent.WaitAsync(_cancellationToken); 
//					_writeEvent.Wait(_cancellationToken);
//					_writeEvent.Reset();
//					continue;
//				}
//
//				var readpos = ReadCursorPosNormalized();
//				int lenToRead = Math.Min(BufferSize - readpos, unread);
//
//				socket.Send(_buffer, readpos, lenToRead, SocketFlags.None);
//
////				 Console.WriteLine("------ > Sent count " + lenToRead);
//
//				_readPosition += lenToRead; // volative write
//
//				_bufferFreeEvent.Set();
//			}
//
//			return Task.CompletedTask;
//		}
//
//		public Task ReceiveFromTask(Socket socket)
//		{
//			if (_writePosition == -1L)
//			{
//				_writePosition = 0L;
//			}
//
//			while (!_cancellationToken.IsCancellationRequested)
//			{
//				// enough room?
//				if (BufferSize - UnreadLength == 0L)
//				{
//					_bufferFreeEvent.Wait(_cancellationToken);
//					_bufferFreeEvent.Reset();
//					// await _bufferFreeEvent.WaitAsync(_cancellationToken);
//					// await _bufferFreeEvent.WaitAsync(_cancellationToken);
//					continue;
//				}
//
//				var writePos = WriteCursorPosNormalized();
//
//				if (BufferSize - writePos == 0L)
//				{
//					// _bufferFreeEvent.Wait(_cancellationToken);
//					continue;
//				}
//
//				var sizeForCopy = Math.Min(BufferSize - writePos, BufferSize);
//
//				var readpos = ReadCursorPosNormalized();
//				if (writePos < readpos && writePos + sizeForCopy > readpos)
//				{
//					// Debug.Assert(false, "will wrap");
//					sizeForCopy = readpos - writePos;
//				}
//
//				// Reads from socket straight into our buffer
//				var actualReceived = socket.Receive(_buffer, writePos, sizeForCopy, SocketFlags.None);
//
//				if (actualReceived == 0)
//					continue;
//
//				// volatile writes
//				_totalLen += actualReceived;
//				_writePosition += actualReceived;
//				// done with writes
//
//				// signal
//				// _writeEvent.Set2();
//				_writeEvent.Set();
//			}
//
//			return Task.CompletedTask;
//		}
//
//		public Task<int> ReadTaskAsync(byte[] buffer, int offset, int count)
//		{
//			var read = this.Read(buffer, offset, count);
//
//			while (read == 0 && !_cancellationToken.IsCancellationRequested)
//			{
//				// _writeEvent.WaitAsync(_cancellationToken);
//				_writeEvent.Wait(_cancellationToken);
//				_writeEvent.Reset();
//				read = this.Read(buffer, offset, count);
//			}
//
//			return Task.FromResult(read);
//		}
//
//		#region Overrides of Stream
//
//		public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
//		{
//			cancellationToken.ThrowIfCancellationRequested();
//			return ReadTaskAsync(buffer, offset, count);
//		}
//
//		public override int Read(byte[] buffer, int offset, int count)
//		{
//			if (_readPosition == -1)
//			{
//				if (_writePosition != -1)
//					_readPosition = 0;
//				else
//					return 0; // nothing was written yet
//			}
//
//			int totalRead = 0;
//			int userBufferRemainLen = count;
//
//			while (totalRead < count)
//			{
//				int unread = UnreadLength();
//				if (unread == 0) break;
//
//				var readpos = ReadCursorPosNormalized();
//				int lenToRead = Math.Min(Math.Min(BufferSize - readpos, unread), userBufferRemainLen);
//
//				Buffer.BlockCopy(_buffer, readpos, buffer, offset, lenToRead);
//
//				_readPosition += lenToRead; // volative write
//
////				if (_isWaiting > 0)
//				{
//					_bufferFreeEvent.Set();
//				}
//
//				offset += lenToRead;
//				userBufferRemainLen -= lenToRead;
//
//				totalRead += lenToRead;
//			}
//
//			return totalRead;
//		}
//
//		/// <summary>
//		/// Presumes no concurrent read access
//		/// </summary>
//		public override long Seek(long offset, SeekOrigin origin)
//		{
//			if (origin == SeekOrigin.Current)
//			{
//				var availableToRead = this.UnreadLength;
//
//				// if we have enough buffer available...
//				if (offset <= availableToRead) 
//				{
//					// checked
//					{
//						_readPosition += offset;
//					}
//					return offset;
//				}
//
//				var remainingToRead = offset;
//
//				while (remainingToRead > 0L)
//				{
//					checked
//					{
//						// move pointer as far as we can
//						_readPosition += availableToRead;
//					}
//
//					remainingToRead -= availableToRead;
//
//					availableToRead = this.UnreadLength;
//
//					if (availableToRead == 0L)
//					{
//						_writeEvent.Wait(_cancellationToken);
////						_writeEvent.Reset();
//					}
//				}
//
//				return offset;
//			}
//			throw new NotSupportedException("Can only seek from current position");
//		}
//
//		public override void SetLength(long value)
//		{
//			throw new NotSupportedException();
//		}
//
//		public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
//		{
//			return Insert(buffer, offset, count);
//		}
//
//		public override void Write(byte[] buffer, int offset, int count)
//		{
//			Insert(buffer, offset, count).Wait();
//		}
//
//		public override bool CanRead
//		{
//			get { return true; }
//		}
//
//		public override bool CanSeek
//		{
//			get { return false; }
//		}
//
//		public override bool CanWrite
//		{
//			get { return true; }
//		}
//
//		public override long Length
//		{
//			get { return GetTotalLength(); }
//		}
//
//		public override long Position
//		{
//			get { return GetReadCursorValue(); }
//			set
//			{
//				throw new NotSupportedException();
//			}
//		}
//
//		public override void Flush()
//		{
//		}
//
//		protected override void Dispose(bool disposing)
//		{
//			base.Dispose(disposing);
//
//			_bufferFreeEvent.Dispose();
//			_writeEvent.Dispose();
//			_buffer = null;
//		}
//
//		#endregion
//
//		private long UnreadLength
//		{
//			[MethodImpl(MethodImplOptions.AggressiveInlining)]
//			get
//			{
//				if (Volatile.Read(ref _readPosition1) == -1L) 
//					return Volatile.Read(ref _totalLen1);
//
//				return (Volatile.Read(ref _totalLen1) - Volatile.Read(ref _readPosition1));
//			}
//		}
//
//		[MethodImpl(MethodImplOptions.AggressiveInlining)]
//		private long GetReadCursorPosNormalized()
//		{
//			return Volatile.Read(ref _readPosition1) % BufferSize;
//		}
//
//		[MethodImpl(MethodImplOptions.AggressiveInlining)]
//		private long GetWriteCursorPosNormalized()
//		{
//			return Volatile.Read(ref _writePosition1) % BufferSize;
//		}
//
//		[MethodImpl(MethodImplOptions.AggressiveInlining)]
//		private long GetTotalLength()
//		{
//			return Volatile.Read(ref _totalLen1);
//		}
//
//		[MethodImpl(MethodImplOptions.AggressiveInlining)]
//		private long GetReadCursorValue()
//		{
//			return Volatile.Read(ref _readPosition1);
//		}
//
//		[MethodImpl(MethodImplOptions.AggressiveInlining)]
//		private long GetWriteCursorValue()
//		{
//			return Volatile.Read(ref _writePosition1);
//		}
//
//		[MethodImpl(MethodImplOptions.AggressiveInlining)]
//		private void SetReadPosition(long newVal)
//		{
//			Volatile.Write(ref _readPosition1, newVal);
//		}
//
//		[MethodImpl(MethodImplOptions.AggressiveInlining)]
//		private void SetWritePosition(long newVal)
//		{
//			Volatile.Write(ref _writePosition1, newVal);
//		}
//
//		[MethodImpl(MethodImplOptions.AggressiveInlining)]
//		private void BlockCopy(byte[] buffer, int offset, int writePos, int sizeForCopy)
//		{
//			// Array.Copy();
//			Buffer.BlockCopy(buffer, offset, _buffer, writePos, sizeForCopy);
//		}
//	}
}