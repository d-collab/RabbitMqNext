namespace RabbitMqNext.Internals.RingBuffer
{
	using System;
	using System.Runtime.CompilerServices;
	using Io;

	public static class Utils
	{
		public static bool IsPowerOfTwo(int n)
		{
			if (n == 1) return false;

			var bitcount = 0;

			for (int i = 0; i < 4; i++)
			{
				var b = (byte) n & 0xFF;

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

	internal class ThreadUtils
	{
		[MethodImpl]
		public static bool IsReadFrameThread()
		{
			var name = System.Threading.Thread.CurrentThread.Name;
			return name != null && name.StartsWith(ConnectionIO.ReadFrameThreadNamePrefix, StringComparison.Ordinal);
		}

//		[MethodImpl]
//		public static bool IsWritingFrameThread()
//		{
//			var name = System.Threading.Thread.CurrentThread.Name;
//			return name != null && name.StartsWith(ConnectionIO.WriteFrameThreadNamePrefix, StringComparison.Ordinal);
//		}
	}
}