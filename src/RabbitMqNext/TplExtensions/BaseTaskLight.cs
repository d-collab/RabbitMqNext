namespace RabbitMqNext.TplExtensions
{
	using System;
	using System.Runtime.CompilerServices;
	using System.Threading.Tasks;

	public class BaseTaskLight<TDerived> : INotifyCompletion, IDisposable
	{
		private readonly Action<TDerived> _recycler;
		private Action _continuation;
		protected volatile bool _isCompleted;
		protected Exception _exception;

		public TDerived GetAwaiter()
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

		public void OnCompleted(Action continuation)
		{
			_continuation = continuation;
		}

		public bool IsCompleted { get { return _isCompleted; } }

		public void SetCompleted(bool runContinuationAsync = false)
		{
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
			if (_recycler != null) _recycler(GetAwaiter());
		}
	}
}