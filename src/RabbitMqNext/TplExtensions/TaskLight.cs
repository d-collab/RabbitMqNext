namespace RabbitMqNext
{
	using System;
	using System.Runtime.CompilerServices;
	using System.Threading;
	using TplExtensions;


	public class TaskLight : BaseTaskLight<TaskLight>, INotifyCompletion
	{
		public TaskLight(Action<TaskLight> recycler) : base(recycler)
		{
		}

		public void OnCompleted(Action continuation)
		{
			SetContinuation(continuation);
		}

		public TaskLight GetAwaiter()
		{
			Console.WriteLine("[TaskLight] GetAwaiter " + " Thread " + Thread.CurrentThread.Name + " " + Thread.CurrentThread.ManagedThreadId); 
			return this;
		}

		public void GetResult()
		{
			if (HasException)
				throw _exception2;
		}
	}
}