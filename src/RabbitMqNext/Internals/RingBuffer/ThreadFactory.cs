namespace RabbitMqNext.Internals.RingBuffer
{
	using System;
	using System.Threading;
	using TplExtensions;

	internal class ThreadFactory
	{
		public static Thread BackgroundThread<T>(Action<T> procStart, string name, T param)
		{
			using (new SuppressFlow())
			{
				var thread = new Thread((tparam) =>
				{
					T arg = (T)tparam;
					procStart(arg);
				})
				{
					IsBackground = true
				};

				if (!string.IsNullOrEmpty(name)) thread.Name = name;

				thread.Start(param);

				return thread;
			}
		}

		public static Thread BackgroundThread(ThreadStart procStart, string name)
		{
			using (new SuppressFlow())
			{
				var thread = new Thread(procStart)
				{
					IsBackground = true
				};
				if (!string.IsNullOrEmpty(name)) thread.Name = name;
			
				thread.Start();

				return thread;
			}
		}
	}
}