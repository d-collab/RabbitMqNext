namespace RabbitMqNext.Tests
{
	using System;
	using System.Threading;
	using System.Threading.Tasks;
	using Internals.RingBuffer;

	public class TestProgram
	{
		public static void Main()
		{
			var totalTasks = 100000;
			var tasks = new TaskSlim<bool>[totalTasks];

			// Initialization
			Action continuation = () =>
			{
				// no-op
			};

			for (int i = 0; i < totalTasks; i++)
			{
				tasks[i] = new TaskSlim<bool>(recycler: null);
				tasks[i].SetContinuation(continuation);
			}

			var res = Parallel.ForEach(tasks, task =>
			{
				try
				{
					task.TrySetResult(true, runContinuationAsync: false);
				}
				catch (Exception e)
				{
					Console.WriteLine("Error  " + e);
				}
			});

			Console.WriteLine("All done");
		}
	}
}
