namespace RabbitMqNext
{
	using System;
	using TplExtensions;


	public class TaskLight : BaseTaskLight<TaskLight>
	{
		public TaskLight(Action<TaskLight> recycler) : base(recycler)
		{
		}

		public void GetResult()
		{
			
		}
	}
}