namespace RabbitMqNext
{
	using System;
	using System.Runtime.CompilerServices;
	using System.Threading;
	using TplExtensions;


	public class TaskSlim : BaseTaskSlim<TaskSlim>, INotifyCompletion
	{
		public TaskSlim(Action<TaskSlim> recycler) : base(recycler)
		{
		}

		public void OnCompleted(Action continuation)
		{
			SetContinuation(continuation);
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