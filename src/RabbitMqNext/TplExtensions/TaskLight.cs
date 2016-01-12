namespace RabbitMqNext
{
	using System;
	using System.Runtime.CompilerServices;
	using TplExtensions;


	public class TaskLight : BaseTaskLight<TaskLight>, INotifyCompletion
	{
		public TaskLight(Action<TaskLight> recycler) : base(recycler)
		{
		}

		public void OnCompleted(Action continuation)
		{
			_continuation = continuation;
		}

		public TaskLight GetAwaiter()
		{
			return this;
		}

		public void GetResult()
		{
		}
	}
}