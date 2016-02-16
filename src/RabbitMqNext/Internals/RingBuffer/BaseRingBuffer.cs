namespace RabbitMqNext.Internals.RingBuffer
{
	using System;
	using System.Runtime.CompilerServices;
	using System.Runtime.InteropServices;
	using System.Threading;
	using Locks;


	internal abstract class BaseRingBuffer
	{
		[StructLayout(LayoutKind.Sequential)]
		internal struct State
		{
			internal PaddingForInt32 _pad0;
			internal volatile uint _readPosition;
			internal uint _readPositionCopy;

			internal PaddingForInt32 _pad1;
			internal volatile uint _writePosition;
			internal uint _writePositionCopy;

			internal PaddingForInt32 _pad2;
			internal uint _bufferSize;
		}

		internal State _state;

		protected readonly CancellationToken _cancellationToken;

		protected readonly AutoResetSuperSlimLock _readLock = new AutoResetSuperSlimLock();
		protected readonly AutoResetSuperSlimLock _writeLock = new AutoResetSuperSlimLock();
		
		const int MaxGates = 64; 
		private readonly ReadingGate[] _gates = new ReadingGate[MaxGates];
		internal long _gateState = 0L;

		private readonly SemaphoreSlim _gateSemaphoreSlim = new SemaphoreSlim(MaxGates, MaxGates);

		private object _gateLocker = new object();

		// adds to the current position
		internal bool TryAddReadingGate(uint length, out ReadingGate gate)
		{
			_gateSemaphoreSlim.Wait();

			lock (_gateLocker)
			{ 
				gate = null;

				if (Volatile.Read(ref _gateState) == -1L)
				{
					return false;
				}

				gate = new ReadingGate
				{
					inEffect = true,
					gpos = _state._readPosition,
					length = length
				};

				AtomicSecureIndexPosAndStore(gate);

			}

			Console.WriteLine("gate added for " + gate.gpos + " len " + gate.length + " at index " + gate.index);
			return true;
		}

		internal void RemoveReadingGate(ReadingGate gate)
		{
			lock (_gateLocker)
			{
				lock (gate)
				{
					if (gate.inEffect == false)
					{

						Console.WriteLine("uaaaaaiiiii " + gate.index);

						_gateSemaphoreSlim.Release();
						return;
					}

					gate.inEffect = false;
					AtomicRemoveAtIndex(gate.index);
				}
			}

			_gateSemaphoreSlim.Release();

			Console.WriteLine("RemoveReadingGate for " + gate.gpos + " at index " + gate.index);

			// if waiting for write coz of gate, given them another chance
			_readLock.Set();
		}

#if !DEBUG
		// [MethodImpl(MethodImplOptions.AggressiveInlining)]
#endif
		internal uint? GetMinReadingGate()
		{
			uint minGatePos = uint.MaxValue;
			bool hadSomeMin = false;

			lock (_gateLocker)
			{
				long oldGateState = 0;
			
				do
				{
					// Possibly ABA problem here!

					oldGateState = Volatile.Read(ref _gateState);
					if (oldGateState == 0L) return null;

					for (int i = 0; i < MaxGates; i++)
					{
						if ((oldGateState & (1L << i)) != 0L)
						{
							var el = _gates[i];
							if (el == null) // race
								continue;
						
							if (el.inEffect) // otherwise ignored
							{
								lock (el)
								if (minGatePos > el.gpos)
								{
									hadSomeMin = true;
									minGatePos = el.gpos;
								}
							}
						}
					}
					// if it changed in the meantime, we need to recalculate
				} while (oldGateState != Volatile.Read(ref _gateState));
			}

			if (hadSomeMin)
				Console.WriteLine("[Min] Min reading gate is " + minGatePos);
			else
				Console.WriteLine("[Min] ---- ");

			if (!hadSomeMin) return null;

			return minGatePos;
		}

#if !DEBUG
		// [MethodImpl(MethodImplOptions.AggressiveInlining)]
#endif
		internal int InternalGetReadyToReadEntries(int desiredCount, out int available, ReadingGate fromGate = null)
		{
			uint bufferSize = _state._bufferSize;

			uint readCursor = _state._readPositionCopy;
			// uint writeCursor = _state._writePosition; // volative read
			uint writeCursor = _state._writePositionCopy;

			if (readCursor == writeCursor ||
			    writeCursor - readCursor >= bufferSize)
			{
				writeCursor = _state._writePosition;
			}

			uint writePos = writeCursor & (bufferSize - 1); // (writeCursor % _bufferSize);
			uint readPos = readCursor & (bufferSize - 1);   // (readCursor % _bufferSize);

			uint entriesFree;

			if (fromGate != null)
			{
				Console.WriteLine("Reading gate index " + fromGate.index + ". ReadCursor g: " + readCursor + " l: readPos " + readPos + 
					" replaced by g: " + fromGate.gpos + " l: " + (fromGate.gpos & (bufferSize - 1)) +
					" diff " + (writeCursor - fromGate.gpos));
				readPos = fromGate.gpos & (bufferSize - 1);
				// entriesFree = fromGate.length;
				desiredCount = Math.Min(desiredCount, (int) fromGate.length);
			}

			// else
			{
				var writeHasWrapped = writePos < readPos;

				if (writeHasWrapped) // so everything ahead of readpos is available
				{
					entriesFree = bufferSize - readPos;
				}
				else
				{
					entriesFree = writePos - readPos;
				}
			}

#if DEBUG
			if (entriesFree > bufferSize)
			{
				var msg = "Assert failed read: " + entriesFree + " must be less or equal to " + (BufferSize);
				System.Diagnostics.Debug.WriteLine(msg);
				throw new Exception(msg);
			}
#endif

			if (fromGate != null)
			{
				available = InternalMin(entriesFree, desiredCount);
			}
			else
			{
				available = InternalMin(entriesFree, desiredCount);
			}

			return (int) readPos;
		}

#if !DEBUG
		// [MethodImpl(MethodImplOptions.AggressiveInlining)]
#endif
		internal int InternalGetReadyToWriteEntries(int desiredCount, out int available)
		{
			var buffersize = _state._bufferSize;

			uint writeCursor = _state._writePositionCopy;
			// uint writeCursor = _state._writePosition; // volative read
			// uint readCursor = _state._readPosition;   // volative read
			uint readCursor = _state._readPositionCopy;

			if (writeCursor == readCursor || 
				writeCursor - readCursor >= buffersize)
			{
				readCursor = _state._readPosition;  // volative read
			}

			uint writePos = writeCursor & (buffersize - 1);
			uint readPos = readCursor & (buffersize - 1);
#if DEBUG
			var originalreadPos = readPos;
#endif

			uint entriesFree = 0;

			// damn gates. 
			var minGate = GetMinReadingGate();
			if (minGate.HasValue) 
			{
				// get min gate index, which becomes essentially the barrier to continue to write
				// what we do is to hide from this operation the REAL readpos

				Console.WriteLine("Writing. gate in place. real g: " + readCursor + " l: " + readPos + 
					" becomes g: " + minGate.Value + " l: " + (minGate.Value & (buffersize - 1)) +
					" and diff " + (writeCursor - minGate.Value));

				readPos = minGate.Value & (buffersize - 1); // now the write cannot move forward
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
					entriesFree = buffersize - writePos - 1;
				else
					entriesFree = buffersize - writePos;
			}

#if DEBUG
			if (writeWrapped)
			{
				if (!(entriesFree <= buffersize - 1))
				{
					var msg = "Assert write1 failed: " + entriesFree + " must be less or equal to " + (BufferSize - 1) +
						" originalreadPos " + originalreadPos + " readpos " + readPos + " write " + writePos +
						" G w " + _state._writePosition + " G r " + _state._readPosition;
					System.Diagnostics.Debug.WriteLine(msg);
					throw new Exception(msg);
				}
			}
			else
			{
				if (!(entriesFree <= buffersize))
				{
					var msg = "Assert write2 failed: " + entriesFree + " must be less or equal to " + (BufferSize);
					System.Diagnostics.Debug.WriteLine(msg);
					throw new Exception(msg);
				}
			}
#endif

			available = InternalMin(entriesFree, desiredCount);
			// return available;
			// return new AvailableAndPos() { available = (int)available, position = (int)writePos };
			return (int) writePos;
		}

		protected BaseRingBuffer(CancellationToken cancellationToken, int bufferSize)
		{
			if (bufferSize <= 0) throw new ArgumentOutOfRangeException("bufferSize");
			if (!Utils.IsPowerOfTwo(bufferSize)) throw new ArgumentException("bufferSize must be multiple of 2", "bufferSize");

			_cancellationToken = cancellationToken;
			_state._bufferSize = (uint)bufferSize;
		}

		public int BufferSize
		{
			get { return (int)_state._bufferSize; }
		}

		public bool HasUnreadContent
		{
			// two volatives reads
			get { return _state._writePosition != _state._readPosition; }
		}

#if !DEBUG
		// [MethodImpl(MethodImplOptions.AggressiveInlining)]
#endif
		private static int InternalMin(uint v1, int v2)
		{
			if (v1 <= v2) 
				return (int)v1;
			return v2;
		}

		private void AtomicSecureIndexPosAndStore(ReadingGate gate)
		{
			while (true)
			{
				long curState = Volatile.Read(ref _gateState); 

				var emptyIndex = -1;

				// find empty spot
				for (int i = 0; i < MaxGates; i++)
				{
					long mask = 1L << i;
					if ((curState & mask) == 0)
					{
						emptyIndex = i; 
						break;
					}
				}

				if (emptyIndex == -1) continue; // try again from the beginning

				long newState = curState | (1L << emptyIndex);

				gate.index = emptyIndex;

#pragma warning disable 420
				if (Interlocked.CompareExchange(ref _gateState, newState, curState) != curState)
#pragma warning restore 420
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
				var curState = Volatile.Read(ref _gateState); // vol read
				var mask = ~(1 << index);
				long newState = curState & mask;

#pragma warning disable 420
				if (Interlocked.CompareExchange(ref _gateState, newState, curState) != curState)
#pragma warning restore 420
				{
					// state was changed. try again
					continue;
				}

				break;
			}
		}

		// For unit testing only

		internal uint GlobalReadPos { get { return _state._readPosition; } }
		internal uint GlobalWritePos { get { return _state._writePosition; } }
		internal uint LocalReadPos { get { return _state._readPosition % _state._bufferSize; } }
		internal uint LocalWritePos { get { return _state._writePosition % _state._bufferSize; } }
	}
}