namespace RabbitMqNext
{
	using System;
	using System.Runtime.CompilerServices;

	public class TaskLight<T> : INotifyCompletion, IDisposable
	{
		private readonly Action<TaskLight<T>> _recycler;
		private Action _continuation;
		private volatile bool _isCompleted = false;
		private Exception _exception;
		private T _result;

		public TaskLight(Action<TaskLight<T>> recycler)
		{
			_recycler = recycler;
		}

		public TaskLight<T> GetAwaiter()
		{
			return this;
		}

		public void Recycle()
		{
			_result = default(T);
			_continuation = null;
			_exception = null;
			_isCompleted = false;
		}

		public void OnCompleted(Action continuation)
		{
			_continuation = continuation;
		}

		public bool IsCompleted { get { return _isCompleted; } }

		public T GetResult()
		{
			if (_exception == null)
			{
				return _result;
			}
			throw _exception;
		}

		public void SetResult(T result)
		{
			_result = result;

			_isCompleted = true;
			var cont = this._continuation;
			if (cont != null)
			{
				cont();
			}
			if (_recycler != null)
			{
				_recycler(this);
			}
		}

		public void SetException(Exception exception)
		{
			_exception = exception;

			_isCompleted = true;
			var cont = this._continuation;
			if (cont != null)
			{
				cont();
			}
			if (_recycler != null)
			{
				_recycler(this);
			}
		}

		public void Dispose()
		{
			_isCompleted = false;
			_continuation = null;
		}
	}

	public class TaskLight : INotifyCompletion, IDisposable
	{
		private readonly Action<TaskLight> _recycler;
		private Action _continuation;
		private volatile bool _isCompleted = false;

		public TaskLight(Action<TaskLight> recycler)
		{
			_recycler = recycler;
		}

		public TaskLight GetAwaiter()
		{
			return this;
		}

		public void Recycle()
		{
			_continuation = null;
			_isCompleted = false;
		}

		public void OnCompleted(Action continuation)
		{
			_continuation = continuation;
		}

		public bool IsCompleted { get { return _isCompleted; } }

		public void GetResult() { }

		public void SetCompleted()
		{
			_isCompleted = true;
			var cont = this._continuation;
			if (cont != null)
			{
				cont();
			}
			if (_recycler != null)
			{
				_recycler(this);
			}
		}

		public void Dispose()
		{
			_isCompleted = false;
			_continuation = null;
		}
	}
}