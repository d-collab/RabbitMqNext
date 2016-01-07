namespace RabbitMqNext.Internals.RingBuffer
{
	using System;
	using System.Threading;
	using System.Threading.Tasks;

	public abstract class Stream2
	{
		// public abstract void Write(byte[] buffer, int offset, int count);

		public abstract Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken);

		public abstract Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken);

		// public abstract ulong Position { get; }
		// public abstract ulong Length { get; }
	}


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
	/// -- write will wrap (n)  :  if r_n > w_n  and (w_n + n > r_n)
	/// -- avail to write bat ^ :  r_n - w_n
	/// 
	///   where n = count 
	/// * must check for wrap first
	/// ^ when restricted by wrap
	/// ]]>
	/// </remarks>
	// TODO: fast modulo of power of twos return i & (n-1); -> http://stackoverflow.com/questions/14997165/fastest-way-to-get-a-positive-modulo-in-c-c
	internal class RingBufferStream2 : IDisposable // Stream2
	{
		// public const long BufferSize = 0x1A0000; // 1703936
		private const int MinBufferSize = 32;

		private readonly byte[] _buffer;

		private volatile uint _readPosition;
		private volatile uint _writePosition;

		private readonly CancellationToken _cancellationToken;
		private readonly WaitingStrategy _waitingStrategy;
		private readonly uint _bufferSize;

		/// <summary>
		/// 
		/// </summary>
		public RingBufferStream2(CancellationToken cancellationToken, 
								 int bufferSize, 
								 WaitingStrategy waitingStrategy)
		{
			if (bufferSize <= 0) throw new ArgumentOutOfRangeException("bufferSize");
			if (bufferSize < MinBufferSize) throw new ArgumentException("buffer must be at least " + MinBufferSize, "bufferSize");
			if (!IsPowerOfTwo(bufferSize)) throw new ArgumentException("bufferSize must be multiple of 2", "bufferSize");

			_cancellationToken = cancellationToken;
			_waitingStrategy = waitingStrategy;
			_bufferSize = (uint) bufferSize;

			_buffer = new byte[bufferSize];

			

		}

		/// <summary>
		/// <![CDATA[
		/// -- avail to write bat * :  BufferSize - w_n
		/// -- write will wrap (n)  :  if r_n > w_n  and (w_n + n > r_n)
		///    where  n = count 
		/// -- avail to write bat ^ :  r_n - w_n
		/// ]]>
		/// </summary>
		public int ClaimWriteRegion(int desiredCount)
		{
			if (desiredCount > _bufferSize) 
				throw new ArgumentOutOfRangeException("desiredCount", "cannot be larger than bufferSize");

//			_cancellationToken.ThrowIfCancellationRequested();

			while (!_cancellationToken.IsCancellationRequested)
			{
				uint writeCursor = _writePosition; // 1 volative read
				uint readCursor = _readPosition;   // 1 volative read

				uint writePos = writeCursor & (_bufferSize - 1); // (writeCursor % _bufferSize); 
				uint readPos = readCursor & (_bufferSize - 1);   // (readCursor % _bufferSize); 

				uint entriesFree = 0;

				var writeWillWrap = (readPos > writePos && writePos + desiredCount > readPos);
				
				if (writeWillWrap)
				{
					var availableTilWrap = readPos - writePos;
					entriesFree = availableTilWrap;
				}
				else
				{
					entriesFree = _bufferSize - writePos;
				}

				if (entriesFree == 0) // full, we need to wait for consumer
				{
					// yield/spin/whatever then...
//					Console.WriteLine("Waiting consumer.");
					_waitingStrategy.Wait();
//					Console.WriteLine("OK1");
					continue;
				}

				return (int) Math.Min(entriesFree, desiredCount);
			}

			return 0; // was cancelled
		}

		/// <summary>
		/// <![CDATA[
		/// -- avail to read bat  * :  w_n - r_n
		/// -- write has wrapped    :  w_n < r_n
		/// -- avail to read bat  ^ :  BufferSize - r_n (for batch)
		/// -- avail to read bat  ^ :  BufferSize - r_n + w_n  (total)
		/// ]]>
		/// </summary>
		public int ClaimReadRegion()
		{
//			_cancellationToken.ThrowIfCancellationRequested();

			while (!_cancellationToken.IsCancellationRequested)
			{
				uint writeCursor = _writePosition; // 1 volative read
				uint readCursor = _readPosition;   // 1 volative read

				uint writePos = writeCursor & (_bufferSize - 1); // (writeCursor % _bufferSize);
				uint readPos = readCursor & (_bufferSize - 1);   // (readCursor % _bufferSize);

				uint entriesFree = 0;

				var readHasWrapped = writePos < readPos;

				if (readHasWrapped)
				{
					entriesFree = _bufferSize - readPos;
				}
				else
				{
					entriesFree = writePos - readPos;
				}

				if (entriesFree == 0) // empty, we need to wait for producer
				{
					// yield/spin/whatever then...
//					Console.WriteLine("Waiting producer.");
					_waitingStrategy.Wait();
//					Console.WriteLine("OK2");
					continue;
				}

				{
					return (int)entriesFree;
				}
			}

			return 0;
		}

		public void WriteToClaimedRegion(byte[] buffer, int offset, int count)
		{
#if DEBUG
			if (offset < 0) throw new ArgumentOutOfRangeException("offset", "must be greater or equal to 0");
			if (count < 0) throw new ArgumentOutOfRangeException("count", "must be greater than 0");
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
			// Buffer.MemoryCopy();? <--- does it need pinning?

			_writePosition += (uint) count; // volative write

			_waitingStrategy.Signal(); // signal - if someone is waiting
		}

		public void ReadClaimedRegion(byte[] buffer, int offset, int count)
		{
#if DEBUG
			if (offset < 0) throw new ArgumentOutOfRangeException("offset", "must be greater or equal to 0");
			if (count < 0) throw new ArgumentOutOfRangeException("count", "must be greater than 0");
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

		public int BufferSize2
		{
			get { return (int) _bufferSize; }
		}

		public void Dispose()
		{
			_waitingStrategy.Dispose();
		}

		private static bool IsPowerOfTwo(int n)
		{
			var bitcount = 0;

			for (int i = 0; i < 4; i++)
			{
				var b = (byte) n & 0xFF;

				for (int j = 0; j < 8; j++)
				{
					var mask = (byte) 1 << j;
					if ((b & mask) != 0)
					{
						if (++bitcount > 1) return false;
					}
				}

				n = n >> 8;
			}
			return (bitcount == 1) ? true : false;
		}
	}
}
