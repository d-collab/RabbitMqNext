namespace RabbitMqNext
{
	using System;
	using System.Runtime.CompilerServices;
	using TplExtensions;

	public class TaskSlim<T> : BaseTaskSlim<TaskSlim<T>>, INotifyCompletion 
	{
		private T _result;

		internal long Started; // for timeout handling
		internal uint Id;  // for correlation checking 

		public TaskSlim(Action<TaskSlim<T>> recycler) : base(recycler)
		{
		}

		public void OnCompleted(Action continuation)
		{
			SetContinuation(continuation);
		}

		public TaskSlim<T> GetAwaiter()
		{
			return this;
		}

		public T GetResult()
		{
			if (HasException) throw _exception;

			// will only be called by Compiler generated code if IsCompleted = true
			return _result; 
		}

		public bool TrySetResult(T result, bool runContinuationAsync = false)
		{
			// Ugh! May overwrite. let's hope the race is between setResult and SetException, not multiples setresult calls. 
			_result = result;
			return TrySetCompleted(runContinuationAsync);
		}

		public override void Recycle()
		{
			base.Recycle();
			_result = default(T);
		}
	}
}