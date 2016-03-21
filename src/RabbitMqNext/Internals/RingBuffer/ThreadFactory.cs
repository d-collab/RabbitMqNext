namespace RabbitMqNext.Internals.RingBuffer
{
	using System;
	using System.Threading;

	internal class ThreadFactory
	{
		public static void BackgroundThread<T>(Action<T> procStart, string name, T param)
		{
			var thread = new Thread((tparam) =>
			{
				T arg = (T) tparam;
				procStart(arg);
			})
			{
				IsBackground = true
			};

			if (!string.IsNullOrEmpty(name)) thread.Name = name;

			thread.Start(param);
		}

		public static void BackgroundThread(ThreadStart procStart, string name)
		{
			var thread = new Thread(procStart)
			{
				IsBackground = true
			};
			if (!string.IsNullOrEmpty(name)) thread.Name = name;
			
			thread.Start();
		}
	}
}