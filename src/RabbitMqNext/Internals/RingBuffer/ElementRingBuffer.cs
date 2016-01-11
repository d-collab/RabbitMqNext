namespace RabbitMqNext.Internals.RingBuffer
{
	using System;
	using System.Collections.Generic;
	using System.Diagnostics;
	using System.Threading;

	internal class ElementRingBuffer<T> : BaseRingBuffer, IDisposable 
		where T : class
	{
		private const int MinBufferSize = 8;
		private const int DefaultBufferSize = 32;

		private readonly T[] _elements;

		public ElementRingBuffer(CancellationToken cancellationToken) : 
			this(cancellationToken, DefaultBufferSize, new LockWaitingStrategy(cancellationToken))
		{
		}

		public ElementRingBuffer(CancellationToken cancellationToken, int bufferSize, WaitingStrategy waitingStrategy)
			: base(cancellationToken, bufferSize, waitingStrategy)
		{
			if (bufferSize < MinBufferSize) throw new ArgumentException("buffer must be at least " + MinBufferSize, "bufferSize");

			_elements = new T[bufferSize];
		}

		public void WriteToNextAvailable(T element)
		{
			if (element == null) Debugger.Break();

			AvailableAndPos availPos;
			while (true)
			{
				availPos = this.InternalGetReadyToWriteEntries(1);
				if (availPos.available != 0) break;
				_waitingStrategy.WaitForRead();
			}

			int writePos = (int)availPos.position;

			// Buffer.BlockCopy(buffer, offset + totalwritten, _buffer, writePos, available);
			_elements[writePos] = element;

			_writePosition += (uint)availPos.available; // volative write

			_waitingStrategy.SignalWriteDone(); // signal - if someone is waiting
		}

		public T GetNextAvailable()
		{
			AvailableAndPos availPos;
			while (true)
			{
				availPos = this.InternalGetReadyToReadEntries((int)_bufferSize);
				if (availPos.available != 0)
					break;
				_waitingStrategy.WaitForWrite();
			}

			int readPos = (int)availPos.position;

//			for (int i = 0; i < availPos.available; i++)
//			{
//				var elem = _elements[i + readPos];
//				yield return elem;
//
//				_readPosition += (uint)1;          // volative write
//				_waitingStrategy.SignalReadDone(); // signal - if someone is waiting
//			}

			var elem = _elements[readPos];
			if (elem == null) Debugger.Break();

			_readPosition += (uint)1;          // volative write
			_waitingStrategy.SignalReadDone(); // signal - if someone is waiting

			return elem;
		}

		public void Dispose()
		{
			_waitingStrategy.Dispose();
		}

	}
}