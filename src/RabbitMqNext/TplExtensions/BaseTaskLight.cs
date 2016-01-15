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
	public class BaseTaskLight<TDerived> : IDisposable
	{
		private readonly Action<TDerived> _recycler;
		protected Action _continuation;
		protected volatile bool _isCompleted;
		protected Exception _exception;

		public TDerived GetDerived()
		{
			return (TDerived) (object)this;
		}

		public BaseTaskLight(Action<TDerived> recycler)
		{
			_recycler = recycler;
		}

		public virtual void Recycle()
		{
			_continuation = null;
			_isCompleted = false;
			_exception = null;
		}

		public bool IsCompleted { get { return _isCompleted; } }

		public void SetCompleted(bool runContinuationAsync = false)
		{
			// we cannot EVER complete more than once. 

			Thread.MemoryBarrier();
			if (_isCompleted) return;

			_isCompleted = true;

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
			else
			{
				DoRecycle();
			}
		}

		public void SetException(Exception exception, bool runContinuationAsync = false)
		{
			_exception = exception;

			SetCompleted(runContinuationAsync);
		}

		public void Dispose()
		{
			_isCompleted = false;
			_continuation = null;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		private void DoRecycle()
		{
			if (_recycler != null) _recycler(GetDerived());
		}
	}
}