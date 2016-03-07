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

	internal static class Utils
	{
		public static bool IsPowerOfTwo(int n)
		{
			var bitcount = 0;

			for (int i = 0; i < 4; i++)
			{
				var b = (byte)n & 0xFF;

				for (int j = 0; j < 8; j++)
				{
					var mask = (byte)1 << j;
					if ((b & mask) != 0)
					{
						if (++bitcount > 1) return false;
					}
				}

				n = n >> 8;
			}
			return bitcount == 1;
		}
	}
}