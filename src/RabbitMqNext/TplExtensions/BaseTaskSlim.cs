namespace RabbitMqNext.TplExtensions
{
	using System;
	using System.Runtime.CompilerServices;
	using System.Threading;
	using System.Threading.Tasks;

	/// <summary>
	/// A "Future" instance that can be recycled and reused, thus alleviating allocs. 
	/// The instance reclycles itself after invoking the continuation. 
	/// </summary>
	/// <remarks>
	/// Upon running the continuation it "recycles" itself (back to pool).
	/// If there's no continuation this instance wont go back to pool.
	/// </remarks>
	public class BaseTaskSlim<TDerived> : IDisposable
	{
		private const byte CompletionStateMask = 0x03;
		private const byte CompletionStatePos = 0;
		private const byte NotCompletedState = 0;
		private const byte CompletedWithResultState = 1;
		private const byte CompletedWithExceptionState = 2;
		private const byte HasContinuationSetMask = 0x04;
		private const byte RunContinuationAsyncMask = 0x08;

		private volatile int _state;

		private readonly Action<TDerived> _recycler;

		protected Action _continuation;
		protected volatile Exception _exception;

		public TDerived GetDerived()
		{
			return (TDerived) (object)this;
		}

		public BaseTaskSlim(Action<TDerived> recycler)
		{
			_recycler = recycler;
		}

		public virtual void Recycle()
		{
			_state = 0;
			_continuation = null;
			_exception = null;
		}

		public bool TrySetCompleted(bool runContinuationAsync = false)
		{
			if (!CompareAndSwap(CompletedWithResultState, NotCompletedState, CompletionStatePos, CompletionStateMask)) return false;

			if (runContinuationAsync)
				RunContinuationAsync = true;

			RunContinuation(runContinuationAsync);

			return true;
		}

		public void SetCompleted(bool runContinuationAsync = false)
		{
			// we cannot EVER complete more than once. 
			if (!CompareAndSwap(CompletedWithResultState, NotCompletedState, CompletionStatePos, CompletionStateMask))
				throw new Exception("Already set as completed");

			if (runContinuationAsync)
				RunContinuationAsync = true;

			RunContinuation(runContinuationAsync);
		}

		public bool TrySetException(Exception exception, bool runContinuationAsync = false)
		{
			if (!CompareAndSwap(CompletedWithExceptionState, NotCompletedState, CompletionStatePos, CompletionStateMask)) return false;

			if (runContinuationAsync)
				RunContinuationAsync = true; // TODO: optimization, add with the TryAtomicXor so single CAS op

			_exception = exception;

			RunContinuation(runContinuationAsync);

			return true;
		}

		public void SetException(Exception exception, bool runContinuationAsync = false)
		{
			if (!CompareAndSwap(CompletedWithExceptionState, NotCompletedState, CompletionStatePos, CompletionStateMask))
				throw new Exception("Already set as completed");

			if (runContinuationAsync)
				RunContinuationAsync = true;  // TODO: optimization, add with the TryAtomicXor so single CAS op

			_exception = exception;

			RunContinuation(runContinuationAsync);
		}

		public void Dispose()
		{
			_state = 0;
			_continuation = null;
			_exception = null;
		}

		public bool IsCompleted
		{
			get { return (_state & CompletedWithResultState) != 0; }
			// ReSharper disable once ValueParameterNotUsed
			// protected set { AtomicChangeState(IsCompleteMask); }
		}

		internal bool HasContinuation
		{
			get { return (_state & HasContinuationSetMask) != 0; }
			// ReSharper disable once ValueParameterNotUsed
			set { AtomicChangeState(HasContinuationSetMask); }
		}

		internal bool RunContinuationAsync
		{
			get { return (_state & RunContinuationAsyncMask) != 0; }
			// ReSharper disable once ValueParameterNotUsed
			set { AtomicChangeState(RunContinuationAsyncMask); }
		}

		internal bool HasException
		{
			get { return (_state & CompletedWithExceptionState) != 0; }
			// ReSharper disable once ValueParameterNotUsed
			// set { AtomicChangeState(HasExceptionMask); }
		}

		internal void SetContinuation(Action continuation)
		{
			if (!HasContinuation)
			{
				_continuation = continuation;
				HasContinuation = true;

				if (IsCompleted || HasException)
				{
					RunContinuation(this.RunContinuationAsync);
				}
			}
			else
			{
				throw new Exception("Very inconsistent state: continuation already set");
			}
		}

		private void RunContinuation(bool runContinuationAsync)
		{
			var cont = this._continuation;
			if (cont != null)
			{
				if (!runContinuationAsync)
				{
					cont();

					DoRecycle();
				}
				else
				{
					Task.Factory.FromAsync(cont.BeginInvoke, cont.EndInvoke, null)
						.ContinueWith((t, pointer) =>
						{
							(pointer as BaseTaskSlim<TDerived>).DoRecycle();

						}, this);
				}
			}
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		private void DoRecycle()
		{
			if (_recycler != null) _recycler(GetDerived());
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		private void AtomicChangeState(byte mask)
		{
			// var spinWait = new SpinWait();
			while (true)
			{
				var curState = _state;
				int newState = curState | mask;

#pragma warning disable 420
				if (Interlocked.CompareExchange(ref _state, newState, curState) == curState)
#pragma warning restore 420
				{
					break;
				}
			}
		}

		/// <summary>
		/// CaS version that preserves the state outside the mask
		/// </summary>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		internal bool CompareAndSwap(int newVal, int comparand, int shifts, int mask)
		{
			var curState = _state;

			// (1) zero the updateBits.  eg oldState = [11111111]    flag=00111000  newState= [11000111]
			// (2) map in the newBits.              eg [11000111] newBits=00101000, newState= [11101111]
			int newState = (curState & ~mask) | (newVal << shifts);
			int expected = (curState & ~mask) | (comparand << shifts);

#pragma warning disable 420
			return (Interlocked.CompareExchange(ref _state, newState, expected) == expected);
#pragma warning restore 420
		}
	}
}