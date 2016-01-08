namespace RabbitMqNext.Internals.RingBuffer
{
	using System;
	using System.Net.Sockets;
	using System.Threading;


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

		private readonly byte[] _buffer;

		private volatile uint _readPosition;
		private volatile uint _writePosition;

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

		public int ClaimWriteRegion()
		{
			return ClaimWriteRegion(BufferSize);
		}

		/// <summary>
		/// <![CDATA[
		/// -- avail to write bat * :  BufferSize - w_n
		/// -- write will wrap (n)  :  if r_n > w_n  and (w_n + n > r_n - 1)
		/// -- avail to write bat ^ :  r_n - w_n - 1
		/// ]]>
		/// </summary>
		public int ClaimWriteRegion(int desiredCount)
		{
			if (desiredCount > _bufferSize) 
				throw new ArgumentOutOfRangeException("desiredCount", "cannot be larger than bufferSize");

//			_cancellationToken.ThrowIfCancellationRequested();

			while (!_cancellationToken.IsCancellationRequested)
			{
				uint writeCursor = _writePosition; // volative read
				uint readCursor = _readPosition;   // volative read

				uint writePos = writeCursor & (_bufferSize - 1); // (writeCursor % _bufferSize); 
				uint readPos = readCursor & (_bufferSize - 1);   // (readCursor % _bufferSize); 

				uint entriesFree = 0;

				var writeWillWrap = (readPos > writePos && writePos + desiredCount > readPos - 1);
				
				if (writeWillWrap)
				{
					var availableTilWrap = readPos - writePos - 1;
					entriesFree = availableTilWrap;
				}
				else
				{
					entriesFree = _bufferSize - writePos;
				}

				if (entriesFree == 0) // full, we need to wait for consumer
				{
					// yield/spin/whatever then...
					_waitingStrategy.Wait();
					continue;
				}

				return (int) Math.Min(entriesFree, desiredCount);
			}

			return 0; // was cancelled
		}

		public int ClaimReadRegion(bool waitIfNothingAvailable = false)
		{
			checked
			{
				return ClaimReadRegion((int)_bufferSize, waitIfNothingAvailable);
			}
		}

		/// <summary>
		/// <![CDATA[
		/// -- avail to read bat  * :  w_n - r_n
		/// -- write has wrapped    :  w_n < r_n
		/// -- avail to read bat  ^ :  BufferSize - r_n (for batch)
		/// -- avail to read bat  ^ :  BufferSize - r_n + w_n  (total)
		/// ]]>
		/// </summary>
		public int ClaimReadRegion(int desiredCount, bool waitIfNothingAvailable = false)
		{
			if (desiredCount > _bufferSize) throw new ArgumentOutOfRangeException("desiredCount", "desiredCount cannot be larger than buffer size: " + desiredCount + " buffer " + _bufferSize);

//			_cancellationToken.ThrowIfCancellationRequested();

			while (!_cancellationToken.IsCancellationRequested)
			{
				uint writeCursor = _writePosition; // 1 volative read
				uint readCursor = _readPosition;   // 1 volative read

				uint writePos = writeCursor & (_bufferSize - 1); // (writeCursor % _bufferSize);
				uint readPos = readCursor & (_bufferSize - 1);   // (readCursor % _bufferSize);

				uint entriesFree = 0;

				var writeHasWrapped = writePos < readPos;

				if (writeHasWrapped) // so everything ahead of readpos is available
				{
					entriesFree = _bufferSize - readPos;
				}
				else
				{
					entriesFree = writePos - readPos;
				}

				if (entriesFree == 0 && waitIfNothingAvailable) // empty, we need to wait for producer
				{
					// yield/spin/whatever then...
//					Console.WriteLine("waiting on producer...");
					_waitingStrategy.Wait();
//					Console.Write(" * ");
					continue;
				}

				return Math.Min(desiredCount, (int)entriesFree);
			}

			return 0;
		}

		public void WriteToClaimedRegion(byte[] buffer, int offset, int count)
		{
#if DEBUG
			if (offset < 0) throw new ArgumentOutOfRangeException("offset", "must be greater or equal to 0");
			if (count <= 0) throw new ArgumentOutOfRangeException("count", "must be greater than 0");
#endif

			uint writeCursor = _writePosition; // 1 volative read
			int writePos = 0;

#if DEBUG
			checked
#endif
			{
				writePos = (int) (writeCursor & (_bufferSize - 1)); // (int) (writeCursor % _bufferSize);
			}

			// TODO: check better options: http://xoofx.com/blog/2010/10/23/high-performance-memcpy-gotchas-in-c/
			Buffer.BlockCopy(buffer, offset, _buffer, writePos, count);

			_writePosition += (uint) count; // volative write

			_waitingStrategy.Signal(); // signal - if someone is waiting
		}

		public void WriteToClaimedRegion(Socket socket, int count)
		{
			uint writeCursor = _writePosition; // volative read
			int writePos = 0;

#if DEBUG
			checked
#endif
			{
				writePos = (int)(writeCursor & (_bufferSize - 1)); // (int) (writeCursor % _bufferSize);
			}

			var received = socket.Receive(_buffer, writePos, count, SocketFlags.None);

			_writePosition += (uint)received; // volative write

			_waitingStrategy.Signal(); // signal - if someone is waiting
		}

		public void ReadClaimedRegion(byte[] buffer, int offset, int count)
		{
#if DEBUG
			if (offset < 0) throw new ArgumentOutOfRangeException("offset", "must be greater or equal to 0");
			if (count <= 0) throw new ArgumentOutOfRangeException("count", "must be greater than 0");
#endif

			uint readCursor = _readPosition; // volative read
			int readPos = 0;
#if DEBUG
			checked 
#endif
			{
				readPos = (int) (readCursor & (_bufferSize - 1)); // (int)(readCursor % _bufferSize);
			}

			Buffer.BlockCopy(_buffer, readPos, buffer, offset, count);
			
			_readPosition += (uint)count; // volative write

			_waitingStrategy.Signal(); // signal - if someone is waiting
		}

		public void ReadClaimedRegion(Socket socket, int count)
		{
			uint readCursor = _readPosition; // volative read
			int readPos = 0;
#if DEBUG
			checked
#endif
			{
				readPos = (int)(readCursor & (_bufferSize - 1)); // (int)(readCursor % _bufferSize);
			}

			var totalSent = 0;
			while (totalSent < count)
			{
				var sent = socket.Send(_buffer, readPos + totalSent, count - totalSent, SocketFlags.None);
				totalSent += sent;
			}

			_readPosition += (uint)totalSent; // volative write

			_waitingStrategy.Signal(); // signal - if someone is waiting

		}

		/// <summary>
		/// count must be the result of the previous call to <see cref="ClaimReadRegion"/>
		/// </summary>
		public void CommitRead(int count)
		{
			if (count <= 0) throw new ArgumentOutOfRangeException("count", "must be greater than 0");

			_readPosition += (uint)count; // volative write
			_waitingStrategy.Signal(); // signal - if someone is waiting
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
	}
}
