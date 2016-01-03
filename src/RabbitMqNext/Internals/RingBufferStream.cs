namespace RabbitMqNext.Internals
{
	using System;
	using System.IO;
	using System.Net.Sockets;
	using System.Runtime.CompilerServices;
	using System.Threading;
	using System.Threading.Tasks;

	internal class RingBufferStream : Stream
	{
		private readonly CancellationToken _cancellationToken;
		public const int BufferSize = 10240;

		private volatile int _totalLen = 0;
		private volatile int _readPosition = -1;
		private volatile int _writePosition = -1;
		private readonly ManualResetEventSlim _bufferFreeEvent = new ManualResetEventSlim(false);
		private readonly AsyncManualResetEvent _writeEvent = new AsyncManualResetEvent();

		private readonly byte[] _buffer;

		public RingBufferStream(CancellationToken cancellationToken)
		{
			_cancellationToken = cancellationToken;
			_buffer = new byte[BufferSize];
		}

		/// <summary>
		/// append param buffer to our ring (single threaded producer)
		/// </summary>
		public void Insert(byte[] buffer, int offset, int original)
		{
			if (original > BufferSize) throw new ArgumentException("overflowing the buffer? nope");
			if (_writePosition == -1)
			{
				_writePosition = 0;
			}

			var totalCopied = 0;
			var userBufferRemainLen = original;

			while (!_cancellationToken.IsCancellationRequested)
			{
//				var readPosAbs = ReadCursorPos();
//				var writePosAbs = WriteCursorPos();
//				var isSane = writePosAbs >= readPosAbs + this.UnreadLength();
//				Debug.Assert(isSane, "not sane");

				// enough room?
				if (BufferSize - UnreadLength() == 0) 
				{
					// Console.WriteLine("waiting");
//					if (_spinWait.NextSpinWillYield)
					{
						_bufferFreeEvent.Wait(_cancellationToken);
						continue;
					}
//					_spinWait.SpinOnce();
//					continue;
				}

				var writePos = WriteCursorPosNormalized();

				var sizeForCopy = Math.Min(BufferSize - writePos, userBufferRemainLen);
				// Debug.Assert(sizeForCopy > 0, "will copy something");

				if (BufferSize - writePos == 0)
				{
					// Console.WriteLine("waiting2");
//					if (_spinWait.NextSpinWillYield)
					{
						_bufferFreeEvent.Wait(_cancellationToken);
						continue;
					}
//					_spinWait.SpinOnce();
//					continue;
				}

				var readpos = ReadCursorPosNormalized();
				if (writePos < readpos && writePos + sizeForCopy > readpos)
				{
					// wrap!
					// Debug.Assert(false, "will wrap");
					sizeForCopy = readpos - writePos;
				}

				Buffer.BlockCopy(buffer, offset, _buffer, writePos, sizeForCopy);
				
				totalCopied += sizeForCopy; // total copied

				// volatile writes
				_totalLen += sizeForCopy;
				_writePosition += sizeForCopy;

				// signal
				_writeEvent.Set2();

				if (totalCopied == original) break; // all copied?

				offset += sizeForCopy; // offset of remainder
				userBufferRemainLen -= sizeForCopy;
			}
		}

		public async Task ReadIntoSocketTask(Socket socket, int count)
		{
			if (_readPosition == -1)
			{
				if (_writePosition != -1)
					_readPosition = 0;
				else
					return ; // nothing was written yet
			}

			int totalRead = 0;
			int userBufferRemainLen = count;

			while (totalRead < count && !_cancellationToken.IsCancellationRequested)
			{
				int unread = UnreadLength();
				if (unread == 0)
				{
					await _writeEvent.WaitAsync(_cancellationToken); 
					continue;
				}

				var readpos = ReadCursorPosNormalized();
				int lenToRead = Math.Min(Math.Min(BufferSize - readpos, unread), userBufferRemainLen);

				socket.Send(_buffer, readpos, lenToRead, SocketFlags.None);

				_readPosition += lenToRead; // volative write

				_bufferFreeEvent.Set();

				userBufferRemainLen -= lenToRead;

				totalRead += lenToRead;
			}

			// return totalRead;
		}

		public async Task ReceiveFromTask(Socket socket)
		{
			if (_writePosition == -1)
			{
				_writePosition = 0;
			}

			while (!_cancellationToken.IsCancellationRequested)
			{
				// enough room?
				if (BufferSize - UnreadLength() == 0)
				{
					_bufferFreeEvent.Wait(_cancellationToken);
					continue;
				}

				var writePos = WriteCursorPosNormalized();

				if (BufferSize - writePos == 0)
				{
					_bufferFreeEvent.Wait(_cancellationToken);
					continue;
				}

				var sizeForCopy = Math.Min(BufferSize - writePos, BufferSize);

				var readpos = ReadCursorPosNormalized();
				if (writePos < readpos && writePos + sizeForCopy > readpos)
				{
					// wrap!
					// Debug.Assert(false, "will wrap");
					sizeForCopy = readpos - writePos;
				}

				// Buffer.BlockCopy(buffer, offset, _buffer, writePos, sizeForCopy);
				var actualReceived = socket.Receive(_buffer, writePos, sizeForCopy, SocketFlags.None);

				// volatile writes
				_totalLen += actualReceived;
				_writePosition += actualReceived;
				// done with writes

				// signal
				_writeEvent.Set2();
			}
		}

		public async Task<int> ReadTaskAsync(byte[] buffer, int offset, int count)
		{
			var read = this.Read(buffer, offset, count);

			while (read == 0 && !_cancellationToken.IsCancellationRequested)
			{
				await _writeEvent.WaitAsync(_cancellationToken);
				read = this.Read(buffer, offset, count);
			}

			return read;
		}

		#region Overrides of Stream

		public override int Read(byte[] buffer, int offset, int count)
		{
			if (_readPosition == -1)
			{
				if (_writePosition != -1)
					_readPosition = 0;
				else
					return 0; // nothing was written yet
			}

			int totalRead = 0;
			int userBufferRemainLen = count;

			while (totalRead < count)
			{
				int unread = UnreadLength();
				if (unread == 0) break;

				var readpos = ReadCursorPosNormalized();
				int lenToRead = Math.Min(Math.Min(BufferSize - readpos, unread), userBufferRemainLen);

				Buffer.BlockCopy(_buffer, readpos, buffer, offset, lenToRead);

				_readPosition += lenToRead; // volative write

//				if (_isWaiting > 0)
				{
					_bufferFreeEvent.Set();
				}

				offset += lenToRead;
				userBufferRemainLen -= lenToRead;

				totalRead += lenToRead;
			}

			return totalRead;
		}

		public override long Seek(long offset, SeekOrigin origin)
		{
			throw new NotSupportedException();
		}

		public override void SetLength(long value)
		{
			throw new NotSupportedException();
		}

		public override void Write(byte[] buffer, int offset, int count)
		{
			Insert(buffer, offset, count);
		}

		public override bool CanRead
		{
			get { return true; }
		}

		public override bool CanSeek
		{
			get { return false; }
		}

		public override bool CanWrite
		{
			get { return true; }
		}

		public override long Length
		{
			get { return _totalLen; }
		}

		public override long Position
		{
			get { return _readPosition; }
			set
			{
				throw new NotSupportedException();
			}
		}

		public override void Flush()
		{
		}

		protected override void Dispose(bool disposing)
		{
			base.Dispose(disposing);

			_bufferFreeEvent.Dispose();
			_writeEvent.Dispose();
		}

		#endregion

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		private int UnreadLength()
		{
			if (_readPosition == -1) return _totalLen;

			return (_totalLen - _readPosition);
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		private int ReadCursorPosNormalized()
		{
			return _readPosition % BufferSize;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		private int WriteCursorPosNormalized()
		{
			return _writePosition % BufferSize;
		}

		
	}
}