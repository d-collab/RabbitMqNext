namespace RabbitMqNext
{
	using System;
	using TplExtensions;

	public class TaskLight<T> : BaseTaskLight<TaskLight<T>>
	{
		private T _result;

		public TaskLight(Action<TaskLight<T>> recycler) : base(recycler)
		{
		}

		public override void Recycle()
		{
			base.Recycle();
			_result = default(T);
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
	}
}