namespace RabbitMqNext
{
	using System;
	using System.Runtime.CompilerServices;
	using TplExtensions;

	public class TaskLight<T> : BaseTaskLight<TaskLight<T>>, INotifyCompletion
	{
		private T _result;

		internal long Started; // for timeout handling
		internal uint Id;  // for correlation checking 

		public TaskLight(Action<TaskLight<T>> recycler) : base(recycler)
		{
		}


		public void OnCompleted(Action continuation)
		{
			_continuation = continuation;
		}

		public TaskLight<T> GetAwaiter()
		{
			return this;
		}

		public T GetResult()
		{
			if (_exception == null) return _result;
			throw _exception;
		}

		public void SetResult(T result, bool runContinuationAsync = false)
		{
			_result = result;

			SetCompleted(runContinuationAsync);
		}

		public override void Recycle()
		{
			base.Recycle();
			_result = default(T);
		}
	}
}