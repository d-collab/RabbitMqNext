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
		private const byte HasContinuationSetMask = 1;
		private const byte IsCompleteMask = 2;
		private const byte HasExceptionMask = 4;
		private const byte RunContinuationAsyncMask = 8;

		private volatile int _state;

		private readonly Action<TDerived> _recycler;
		protected Action _continuation;
		protected Exception _exception;

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

		public void SetCompleted(bool runContinuationAsync = false)
		{
			if (runContinuationAsync)
				RunContinuationAsync = true;

			// we cannot EVER complete more than once. 
			if (IsCompleted) throw new Exception("Already set as completed");
			
			IsCompleted = true;

			RunContinuation(runContinuationAsync);
		}

		public void SetException(Exception exception, bool runContinuationAsync = false)
		{
			if (runContinuationAsync)
				RunContinuationAsync = true;

			_exception = exception;
			HasException = true;

			SetCompleted(runContinuationAsync);
		}

		public void Dispose()
		{
			_state = 0;
			_continuation = null;
			_exception = null;
		}

		public bool IsCompleted
		{
			get { return (_state & IsCompleteMask) != 0; }
			// ReSharper disable once ValueParameterNotUsed
			protected set { AtomicChangeState(IsCompleteMask); }
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
			get { return (_state & HasExceptionMask) != 0; }
			// ReSharper disable once ValueParameterNotUsed
			set { AtomicChangeState(HasExceptionMask); }
		}

		internal void SetContinuation(Action continuation)
		{
			if (!HasContinuation)
			{
				_continuation = continuation;
				HasContinuation = true;

				if (IsCompleted)
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
						.ContinueWith(t =>
						{
							DoRecycle();
						});
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
//				spinWait.SpinOnce();
			}
		}
	}
}