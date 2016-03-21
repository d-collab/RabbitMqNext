namespace RabbitMqNext.Internals.RingBuffer
{
	public static class Utils
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