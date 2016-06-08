namespace RabbitMqNext
{
	using System;
	using System.Runtime.CompilerServices;
	using TplExtensions;


	public class TaskSlim : BaseTaskSlim<TaskSlim>, INotifyCompletion // ICriticalNotifyCompletion
	{
		public TaskSlim(Action<TaskSlim> recycler) : base(recycler)
		{
		}

		public void OnCompleted(Action continuation)
		{
			SetContinuation(continuation);
		}

		public void UnsafeOnCompleted(Action continuation)
		{
			this.OnCompleted(continuation);
		}

		public TaskSlim GetAwaiter()
		{
			return this;
		}

		public void GetResult()
		{
			if (HasException) throw _exception;
		}
	}
}