namespace RabbitMqNext
{
	using System;
	using System.Runtime.CompilerServices;

	public class TaskLight : INotifyCompletion, IDisposable
	{
		private readonly Action<TaskLight> _recycler;
		private Action _continuation;
		private bool _isCompleted = false;

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