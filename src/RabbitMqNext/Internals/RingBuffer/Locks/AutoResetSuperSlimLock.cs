namespace RabbitMqNext.Internals.RingBuffer.Locks
{
	using System;
	using System.Collections.Concurrent;
	using System.Collections.Generic;
	using System.Runtime.CompilerServices;
	using System.Threading;
	using System.Threading.Tasks;


	/// <summary>
	/// heavily "inspired" by the counterpart ManualResetEventSlim
	/// with the difference that waiters try to atomically "unset" the set bit/flag
	/// </summary>
	internal class AutoResetSuperSlimLock
	{
		internal const int SignalledStateMask = 0x8000;      // 1000 0000 0000 0000
		internal const int SignalledStatePos = 15;
		internal const int NumWaitersStateMask = (0xFF);     // 0000 0000 1111 1111
		internal const int NumWaitersStatePos = 0;
//		internal const int RelWaitersStateMask = (0x7F00);   // 0111 1111 0000 0000
//		internal const int RelWaitersStatePos = 8;

		private readonly ConcurrentQueue<TaskCompletionSource<bool>> _waiters = new ConcurrentQueue<TaskCompletionSource<bool>>();
		private volatile int _state;
		private readonly object _lock = new object();
		
		private static readonly int ProcCounter = Environment.ProcessorCount;

		private const int HowManySpinBeforeYield = 10;
		private const int HowManyYieldEverySleep0 = 5;
		private const int HowManyYieldEverySleep1 = 20;
		private const int SpinCount = 50;

		public AutoResetSuperSlimLock(bool initialState = false)
		{
			if (initialState) _state = SignalledStateMask;
		}

		public Task WaitAsync()
		{
			return WaitAsync(Timeout.Infinite);
		}

		public Task WaitAsync(int millisecondsTimeout) //, CancellationToken cancellationToken)
		{
//			cancellationToken.Register(OnCancelled)

			if (!CheckForIsSetAndResetIfTrue())
			{
				if (SpinAndTryToObtainLock()) 
					return Task.CompletedTask;

				var tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
				_waiters.Enqueue(tcs);
			}

			return Task.CompletedTask;
		}

		public bool Wait()
		{
			return Wait(Timeout.Infinite);
		}

		public bool Wait(int millisecondsTimeout)
		{
			if (!CheckForIsSetAndResetIfTrue())
			{
				if (SpinAndTryToObtainLock()) 
					return true;

				lock (_lock)
				{
					// if (!CheckForIsSetAndResetIfTrue())
					{
						Waiters++;

						if (CheckForIsSetAndResetIfTrue())
						{
							Waiters--;
							return true;
						}

						try
						{
							while (true)
							{
								if (!Monitor.Wait(_lock, millisecondsTimeout))
								{
									return false; // timeout expired
								}
								else
								{
									if (CheckForIsSetAndResetIfTrue())
										break;
								}
							}
						}
						finally
						{
							Waiters--;
						}
					}
				}
			}

			return true;
		}

		public void Set()
		{
			AtomicChange(1, SignalledStatePos, SignalledStateMask);

			List<TaskCompletionSource<bool>> tcss = null;

			lock (_lock)
			{
				if (Waiters > 0)
				{
					Monitor.Pulse(_lock);
				}
				else
				{
					do
					{
						TaskCompletionSource<bool> tcs;
						if (_waiters.TryDequeue(out tcs))
						{
							if (tcss == null) tcss = new List<TaskCompletionSource<bool>>();
							AtomicChange(0, SignalledStatePos, SignalledStateMask);
							tcss.Add(tcs);
						}
						else break;

					} while (!_waiters.IsEmpty);
				}
			}
			// schedules the continuation to run
			// since it was created with TaskCreationOptions.RunContinuationsAsynchronously
			// it's guaranteed to run outside this _lock, but just in case...
			if (tcss != null)
			{
				foreach (var tcs in tcss)
					tcs.SetResult(true);
			}
		}

		public bool IsSet
		{
			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			get { return (_state & SignalledStateMask) != 0; }
		}

		public void Dispose()
		{
			TaskCompletionSource<bool> tcs;
			if (_waiters.TryDequeue(out tcs))
				tcs.SetCanceled();
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		private bool CheckForIsSetAndResetIfTrue()
		{
			return TryAtomicXor(0, SignalledStatePos, SignalledStateMask);
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		internal void AtomicChange(int val, int shifts, int mask)
		{
			var spinWait = new SpinWait();
			while (true)
			{
				var curState = _state;

				// (1) zero the updateBits.  eg oldState = [11111111]    flag=00111000  newState= [11000111]
				// (2) map in the newBits.              eg [11000111] newBits=00101000, newState= [11101111]
				int newState = (curState & ~mask) | (val << shifts);

#pragma warning disable 420
				if (Interlocked.CompareExchange(ref _state, newState, curState) == curState)
#pragma warning restore 420
				{
					break;
				}

				spinWait.SpinOnce();
			}
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		internal bool TryAtomicXor(int val, int shifts, int mask)
		{
			var curState = _state;

			// (1) zero the updateBits.  eg oldState = [11111111]    flag=00111000  newState= [11000111]
			// (2) map in the newBits.              eg [11000111] newBits=00101000, newState= [11101111]
			int newState = (curState & ~mask) |  (val << shifts);

			// (1) zero the updateBits.  eg oldState = [11111111]    flag=00111000  newState= [11000111]
			// (2) map in the newBits.              eg [11000111] newBits=00101000, newState= [11101111]
			int expected = (newState ^ mask) | curState;

			// newState [100001]
			// expected [000001]

#pragma warning disable 420
			return (Interlocked.CompareExchange(ref _state, newState, expected) == expected);
#pragma warning restore 420
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		private bool SpinAndTryToObtainLock()
		{
			for (int i = 0; i < SpinCount; i++)
			{
				if (CheckForIsSetAndResetIfTrue())
				{
					return true;
				}
				if (i < HowManySpinBeforeYield)
				{
					if (i == HowManySpinBeforeYield / 2)
					{
						Thread.Yield();
					}
					else
					{
						Thread.SpinWait(ProcCounter * (4 << i));
					}
				}
				else if (i % HowManyYieldEverySleep1 == 0)
				{
					Thread.Sleep(1);
				}
				else if (i % HowManyYieldEverySleep0 == 0)
				{
					Thread.Sleep(0);
				}
				else
				{
					Thread.Yield();
				}
			}
			return CheckForIsSetAndResetIfTrue();
		}

		internal int Waiters
		{
			get
			{
				return ExtractStatePortionAndShiftRight(_state, NumWaitersStateMask, NumWaitersStatePos);
			}
			set
			{
				AtomicChange(value, NumWaitersStatePos, NumWaitersStateMask);
			}
		}

//		internal int ToRelease
//		{
//			get
//			{
//				var val = ExtractStatePortionAndShiftRight(_state, RelWaitersStateMask, RelWaitersStatePos);
//				return val;
//			}
//			set
//			{
//				AtomicChange(value, RelWaitersStatePos, RelWaitersStateMask);
//			}
//		}

		private static int ExtractStatePortionAndShiftRight(int state, int mask, int rightBitShiftCount)
		{
			//convert to uint before shifting so that right-shift does not replicate the sign-bit,
			//then convert back to int.
			return unchecked((int)(((uint)(state & mask)) >> rightBitShiftCount));
		}
	}
}