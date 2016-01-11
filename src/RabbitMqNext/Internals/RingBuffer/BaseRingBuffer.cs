namespace RabbitMqNext.Internals.RingBuffer
{
	using System;
	using System.Runtime.CompilerServices;
	using System.Threading;


	internal class ReadingGate
	{
		public volatile bool inEffect = true;
		public uint pos;
		public int index;
	}

	internal abstract class BaseRingBuffer
	{
		// TODO: how to add padding to ensure these go and stay in L1 cache? how to debug it?
		internal volatile uint _readPosition; 
		internal volatile uint _writePosition;

		protected readonly CancellationToken _cancellationToken;
		protected readonly WaitingStrategy _waitingStrategy;
		protected readonly uint _bufferSize;
		
		// max gates = 256
		const int MaxGates = 32; 
		private readonly ReadingGate[] _gates = new ReadingGate[MaxGates];
		private volatile int _gateState = 0; // 11111111 11111111 11111111 11111111

		// adds to the current position
		internal ReadingGate AddReadingGate()
		{
			if (_gateState == -1) throw new Exception("Max gates reached");

			var gate = new ReadingGate() { pos = _readPosition };

			AtomicSecureIndexPosAndStore(gate);

			return gate;
		}

		internal void RemoveReadingGate(ReadingGate gate)
		{
			gate.inEffect = false;

			AtomicRemoveAtIndex(gate.index);

			// if waiting for write coz of gate, given then another chance
			_waitingStrategy.SignalReadDone();
		}

		internal uint? GetMinReadingGate()
		{
			uint minGatePos = uint.MaxValue;

			var oldGateState = _gateState;
			do
			{
				if (oldGateState == 0) return null;

				for (int i = 0; i < MaxGates; i++)
				{
					if ((oldGateState & (1 << i)) != 0)
					{
						var el = _gates[i];
						if (el == null) // race
							continue;
						if (el.inEffect) // otherwise ignored
						{
							if (minGatePos > el.pos)
								minGatePos = el.pos;
						}
					}
				}
				// if it changed in the meantime, we need to recalculate
			} while (oldGateState != _gateState); 

			return minGatePos;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		internal AvailableAndPos InternalGetReadyToReadEntries(int desiredCount, ReadingGate fromGate = null)
		{
			uint writeCursor = _writePosition; // volative read
			uint readCursor = _readPosition;   // volative read

			uint writePos = writeCursor & (_bufferSize - 1); // (writeCursor % _bufferSize);
			uint readPos = readCursor & (_bufferSize - 1);   // (readCursor % _bufferSize);

			if (fromGate != null)
			{
				Console.WriteLine("Reading from gate. Real readpos " +  readPos + " replaced by " + fromGate.pos);
				readPos = fromGate.pos;
			}

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

			// damn gates. 
			var minGate = GetMinReadingGate();
			if (minGate.HasValue) 
			{
				// get min gate index, which becomes essentially the barrier to continue to write
				// what we do is to hide from this operation the REAL readpos

				Console.WriteLine("Got gate in place. real readPos " + readPos + " becomes " + minGate.Value);

				readPos = minGate.Value; // now the write cannot move forward
			} 

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

		private void AtomicSecureIndexPosAndStore(ReadingGate gate)
		{
			while (true)
			{
				var curState = _gateState; // vol read
				var emptyIndex = -1;

				// find empty spot
				for (var i = 0; i < MaxGates; i++)
				{
					int mask = 1 << i;
					if ((curState & mask) == 0)
					{
						emptyIndex = i; 
						break;
					}
				}

				if (emptyIndex == -1) continue; // try again from the beginning

				int newState = curState | (1 << emptyIndex);

				gate.index = emptyIndex;

				if (Interlocked.CompareExchange(ref _gateState, newState, curState) != curState)
				{
					// state was changed. try again
					continue;
				}

				_gates[emptyIndex] = gate; // race between changing the state and saving to array.
				break;
			}
		}

		private void AtomicRemoveAtIndex(int index)
		{
			while (true)
			{
				var curState = _gateState; // vol read
				var mask = ~(1 << index);
				int newState = curState & mask;

				if (Interlocked.CompareExchange(ref _gateState, newState, curState) != curState)
				{
					// state was changed. try again
					continue;
				}

				break;
			}
		}

		// For unit testing only

		internal uint GlobalReadPos { get { return _readPosition; } }
		internal uint GlobalWritePos { get { return _writePosition; } }
		internal uint LocalReadPos { get { return _readPosition % _bufferSize; } }
		internal uint LocalWritePos { get { return _writePosition % _bufferSize; } }

	}
}