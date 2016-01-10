namespace RabbitMqNext.Internals.RingBuffer
{
	using System;
	using System.Diagnostics;
	using System.IO;
	using System.Net.Sockets;
	using System.Runtime.CompilerServices;
	using System.Text;
	using System.Threading;
	using System.Threading.Tasks;


	/// <summary>
	/// 
	/// </summary>
	/// <remarks>
	/// <![CDATA[
	/// Proof it will work even when the cursor overflows
	/// * Preconditions: Buffer < bounds of cursor, so modulus operation work
	/// * cursors are unsigned. overflows are allowed and accounted for
	/// * operations on buffer are batched 
	///   - if you want to write 20 bytes, but there's only 10 positions 
	///     til end, and 10 positions free in the beginning, then 2 batch operations will take place)
	/// 
	/// writeCur : uint = w
	/// readCur  : uint = r
	/// 
	/// normalizedWrite = writeCur % BufferSize  = w_n
	/// normalizedRead  = readCur  % BufferSize  = r_n
	/// 
	/// == Formalization of per read / write cursors == 
	///    [ Bound to range of cursor, in this case uint 0..2^32 ]
	///    
	/// -- available to read    :  w - r
	/// -- available to write   :  r - w - 1 
	/// 
	/// == Formalization of per read / write batches
	///    [ Bound to 0..BufferSize ]
	/// 
	/// -- avail to read bat  * :  w_n - r_n
	/// -- write has wrapped    :  w_n < r_n
	/// -- avail to read bat  ^ :  BufferSize - r_n (for batch)
	/// -- avail to read bat  ^ :  BufferSize - r_n + w_n  (total)
	/// 
	/// -- avail to write bat * :  BufferSize - w_n
	/// -- write will wrap (n)  :  if r_n > w_n  and (w_n + n > r_n - 1)
	/// -- avail to write bat ^ :  r_n - w_n - 1
	/// 
	///   where n = count 
	/// * must check for wrap first
	/// ^ when restricted by wrap
	/// ]]>
	/// </remarks>
	internal class RingBuffer2 : IDisposable // Stream2
	{
		private const int MinBufferSize = 32;
		private const int DefaultBufferSize = 0x100000;     //  1.048.576 1mb
		// private const int DefaultBufferSize = 0x200000;  //  2.097.152 2mb
		// private const int DefaultBufferSize = 0x20000;   //    131.072
		// private const int DefaultBufferSize = 0x10000;   //     65.536

		private readonly byte[] _buffer;

		internal volatile uint _readPosition;
		internal volatile uint _writePosition;

		private readonly CancellationToken _cancellationToken;
		private readonly WaitingStrategy _waitingStrategy;
		private readonly uint _bufferSize;

		/// <summary>
		/// Creates with default buffer size and 
		/// default locking strategy
		/// </summary>
		/// <param name="cancellationToken"></param>
		public RingBuffer2(CancellationToken cancellationToken) : this(cancellationToken, DefaultBufferSize, new LockWaitingStrategy(cancellationToken))
		{
		}

		/// <summary>
		/// 
		/// </summary>
		public RingBuffer2(CancellationToken cancellationToken, int bufferSize, WaitingStrategy waitingStrategy)
		{
			if (bufferSize <= 0) throw new ArgumentOutOfRangeException("bufferSize");
			if (bufferSize < MinBufferSize) throw new ArgumentException("buffer must be at least " + MinBufferSize, "bufferSize");
			if (!Utils.IsPowerOfTwo(bufferSize)) throw new ArgumentException("bufferSize must be multiple of 2", "bufferSize");

			_cancellationToken = cancellationToken;
			_waitingStrategy = waitingStrategy;
			_bufferSize = (uint) bufferSize;

			_buffer = new byte[bufferSize];
		}

		// TODO: check better options: http://xoofx.com/blog/2010/10/23/high-performance-memcpy-gotchas-in-c/
		public int Write(byte[] buffer, int offset, int count, bool writeAll = true)
		{
#if DEBUG
			if (offset < 0) throw new ArgumentOutOfRangeException("offset", "must be greater or equal to 0");
			if (count <= 0) throw new ArgumentOutOfRangeException("count", "must be greater than 0");
#endif

			int totalwritten = 0;
			while (totalwritten < count)
			{
				AvailableAndPos availPos = this.InternalGetReadyToWriteEntries(count - totalwritten);
//				var available = (int) this.InternalGetReadyToWriteEntries(count - totalwritten);
				var available = (int) availPos.available;
				if (available == 0)
				{
					if (writeAll)
					{
						_waitingStrategy.WaitForRead();
						continue;
					}
					break;
				}

				int writePos = (int) availPos.position;

				Buffer.BlockCopy(buffer, offset + totalwritten, _buffer, writePos, available);

				totalwritten += available;

				_writePosition += (uint)available; // volative write

				_waitingStrategy.SignalWriteDone(); // signal - if someone is waiting
			}

			return totalwritten;
		}

		public Task<int> WriteAsync(byte[] buffer, int offset, int count, bool writeAll,
			CancellationToken cancellationToken)
		{
			var written = Write(buffer, offset, count, writeAll: true);
			return Task.FromResult(written);
		}

		public async Task WriteBufferFromSocketRecv(Socket socket, bool asyncRecv = false)
		{
			AvailableAndPos availPos;
			int available = 0;
			
			while (true)
			{
				availPos = this.InternalGetReadyToWriteEntries(BufferSize);
				// available = (int) this.InternalGetReadyToWriteEntries(BufferSize);
				available = availPos.available;

				if (available == 0)
					_waitingStrategy.WaitForRead();
				else
					break;
			}

			int writePos = availPos.position;

			int received = 0;
			if (!asyncRecv)
			{
				received = socket.Receive(_buffer, writePos, available, SocketFlags.None);
			}
			else
			{
				received = await socket.ReceiveTaskAsync(_buffer, writePos, available);
			}

			_writePosition += (uint)received; // volative write

			_waitingStrategy.SignalWriteDone(); // signal - if someone is waiting
		}

		public int Read(byte[] buffer, int offset, int count, bool fillBuffer = false)
		{
#if DEBUG
			if (offset < 0) throw new ArgumentOutOfRangeException("offset", "must be greater or equal to 0");
			if (count <= 0) throw new ArgumentOutOfRangeException("count", "must be greater than 0");
#endif

			int totalRead = 0;

			while (totalRead < count)
			{
				AvailableAndPos availPos = this.InternalGetReadyToReadEntries(count - totalRead);
				// var available = (int) this.InternalGetReadyToReadEntries(count - totalRead);
				var available = (int) availPos.available;
				if (available == 0)
				{
					if (fillBuffer)
					{
						_waitingStrategy.WaitForWrite();
						continue;
					} 
					break;
				}

				int readPos = (int) availPos.position;

				Buffer.BlockCopy(_buffer, readPos, buffer, offset + totalRead, available);

				totalRead += available;

				// if (!fillBuffer) break;
				_readPosition += (uint)available; // volative write

				_waitingStrategy.SignalReadDone(); // signal - if someone is waiting
			}

			return totalRead;
		}

		public async Task<int> ReadAsync(byte[] buffer, int offset, int count, bool fillBuffer, CancellationToken cancellationToken)
		{
#if DEBUG
			if (offset < 0) throw new ArgumentOutOfRangeException("offset", "must be greater or equal to 0");
			if (count <= 0) throw new ArgumentOutOfRangeException("count", "must be greater than 0");
#endif

			int totalRead = 0;

			while (totalRead < count)
			{
				AvailableAndPos availPos = this.InternalGetReadyToReadEntries(count - totalRead);
				// var available = (int) this.InternalGetReadyToReadEntries(count - totalRead);
				var available = (int)availPos.available;
				if (available == 0)
				{
					if (fillBuffer)
					{
						await _waitingStrategy.WaitForWriteAsync();
						continue;
					}
					break;
				}

				int readPos = (int)availPos.position;

				Buffer.BlockCopy(_buffer, readPos, buffer, offset + totalRead, available);

				totalRead += available;

				// if (!fillBuffer) break;
				_readPosition += (uint)available; // volative write

				_waitingStrategy.SignalReadDone(); // signal - if someone is waiting
			}

			return totalRead;
		}

		public async Task ReadBufferIntoSocketSend(Socket socket, bool asyncSend)
		{
			int totalRead = 0;

			while (totalRead == 0)
			{
				AvailableAndPos availPos = this.InternalGetReadyToReadEntries(BufferSize);
				int available = availPos.available;
//				var available = (int)this.InternalGetReadyToReadEntries(BufferSize);
				if (available == 0)
				{
					_waitingStrategy.WaitForWrite();
					continue;
				}

				int readPos = availPos.position;

				var totalSent = 0;
				while (totalSent < available)
				{
					var sent = 0;
					if (!asyncSend)
					{
						sent = socket.Send(_buffer, readPos + totalSent, available - totalSent, SocketFlags.None);
					}
					else
					{
						sent = await socket.SendTaskAsync(_buffer, readPos + totalSent, available - totalSent);
					}

					totalSent += sent;
				}

				// maybe better throughput if this goes inside the inner loop
				_readPosition += (uint)totalSent; // volative write
				_waitingStrategy.SignalReadDone(); // signal - if someone is waiting

				totalRead += available;
			}
		}

		public Task Skip(int offset)
		{
			int totalSkipped = 0;

			while (totalSkipped < offset)
			{
				AvailableAndPos availPos = this.InternalGetReadyToReadEntries(offset - totalSkipped);
				var available = availPos.available;
				// var available = (int)this.InternalGetReadyToReadEntries(offset - totalSkipped);
				if (available == 0)
				{
					_waitingStrategy.WaitForWrite();
					continue;
				}

				totalSkipped += available;

				_readPosition += (uint)available; // volative write

				_waitingStrategy.SignalReadDone(); // signal - if someone is waiting
			}

			return Task.CompletedTask;
		}

		public int BufferSize
		{
			get { return (int) _bufferSize; }
		}

		public CancellationToken CancellationToken { get { return _cancellationToken; } }

		public bool HasUnreadContent
		{
			// two volatives reads
			get { return _writePosition != _readPosition; }
		}

		public void Dispose()
		{
			_waitingStrategy.Dispose();
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		internal AvailableAndPos InternalGetReadyToReadEntries(int desiredCount)
		{
			uint writeCursor = _writePosition; // volative read
			uint readCursor = _readPosition;   // volative read

			uint writePos = writeCursor & (_bufferSize - 1); // (writeCursor % _bufferSize);
			uint readPos = readCursor & (_bufferSize - 1);   // (readCursor % _bufferSize);

			uint entriesFree;

			var writeHasWrapped = writePos < readPos;

			if (writeHasWrapped) // so everything ahead of readpos is available
			{
				entriesFree = _bufferSize - readPos;
			}
			else
			{
				entriesFree = writePos - readPos;
			}

#if DEBUG
			if (entriesFree > _bufferSize)
			{
				var msg = "Assert failed read: " + entriesFree + " must be less or equal to " + (BufferSize);
				System.Diagnostics.Debug.WriteLine(msg);
				throw new Exception(msg);
			}
#endif

			var available = Math.Min(entriesFree, (uint)desiredCount);
			// return available;
			return new AvailableAndPos() { available = (int)available, position = (int)readPos };
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		internal AvailableAndPos InternalGetReadyToWriteEntries(int desiredCount)
		{
			uint writeCursor = _writePosition; // volative read
			uint readCursor = _readPosition;   // volative read

			uint writePos = writeCursor & (_bufferSize - 1); 
			uint readPos = readCursor & (_bufferSize - 1);  

			uint entriesFree = 0;

			// var writeWillWrap = (readPos > writePos && writePos + desiredCount > readPos - 1);
			var writeWrapped = readPos > writePos;

			if (writeWrapped)
			{
				var availableTilWrap = readPos - writePos - 1;
				entriesFree = availableTilWrap;
			}
			else
			{
				if (readPos == 0)
					entriesFree = _bufferSize - writePos - 1;
				else
					entriesFree = _bufferSize - writePos;
			}

#if DEBUG
			if (writeWrapped)
			{
				if (!(entriesFree <= _bufferSize - 1))
				{
					var msg = "Assert write1 failed: " + entriesFree + " must be less or equal to " + (BufferSize - 1);
					System.Diagnostics.Debug.WriteLine(msg);
					throw new Exception(msg);
				}
			}
			else
			{
				if (!(entriesFree <= _bufferSize))
				{
					var msg = "Assert write2 failed: " + entriesFree + " must be less or equal to " + (BufferSize);
					System.Diagnostics.Debug.WriteLine(msg);
					throw new Exception(msg);
				}
			}
#endif

			var available = Math.Min(entriesFree, (uint) desiredCount);
//			return available;
			return new AvailableAndPos() { available = (int)available, position = (int)writePos };
		}

		internal struct AvailableAndPos
		{
			public int available, position;
		}

		// For unit testing only

		internal uint GlobalReadPos { get { return _readPosition; } }
		internal uint GlobalWritePos { get { return _writePosition; } }
		internal uint LocalReadPos { get { return _readPosition % _bufferSize; } }
		internal uint LocalWritePos { get { return _writePosition % _bufferSize; } }
	}
}
