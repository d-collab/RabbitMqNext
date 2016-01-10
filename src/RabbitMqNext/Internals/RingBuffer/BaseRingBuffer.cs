namespace RabbitMqNext.Internals.RingBuffer
{
	using System;
	using System.Runtime.CompilerServices;
	using System.Threading;

	internal abstract class BaseRingBuffer
	{
		internal volatile uint _readPosition;
		internal volatile uint _writePosition;

		protected readonly CancellationToken _cancellationToken;
		protected readonly WaitingStrategy _waitingStrategy;
		protected readonly uint _bufferSize;

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

			var available = Math.Min(entriesFree, (uint)desiredCount);
			//			return available;
			return new AvailableAndPos() { available = (int)available, position = (int)writePos };
		}

		protected BaseRingBuffer(CancellationToken cancellationToken, int bufferSize, WaitingStrategy waitingStrategy)
		{
			if (bufferSize <= 0) throw new ArgumentOutOfRangeException("bufferSize");
			if (!Utils.IsPowerOfTwo(bufferSize)) throw new ArgumentException("bufferSize must be multiple of 2", "bufferSize");

			_cancellationToken = cancellationToken;
			_waitingStrategy = waitingStrategy;
			_bufferSize = (uint)bufferSize;
		}

		public int BufferSize
		{
			get { return (int)_bufferSize; }
		}

		public bool HasUnreadContent
		{
			// two volatives reads
			get { return _writePosition != _readPosition; }
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