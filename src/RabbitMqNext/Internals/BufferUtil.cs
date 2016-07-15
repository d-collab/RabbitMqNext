namespace RabbitMqNext.Internals
{
	using System;
	using System.Runtime.CompilerServices;
	using RingBuffer;

	public static class BufferUtil
	{
		private static readonly byte[] Empty = new byte[0];

		public static byte[] Copy(RingBufferStreamAdapter stream, int bodySize)
		{
			if (bodySize == 0) return Empty;

			var buffer = new byte[bodySize]; // more allocations. sad!
			var read = stream.Read(buffer, 0, (int)bodySize, fillBuffer: true);
			
			if (read != bodySize) throw new Exception("Read less than body size");

			return buffer;
		}

		public static byte[] Copy(MultiBodyStreamWrapper stream, int bodySize)
		{
			if (bodySize == 0) return Empty;

			var buffer = new byte[bodySize]; // more allocations. sad!
			stream.ReadAllInto(buffer, 0, bodySize);

			return buffer;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		static unsafe void CustomCopy(void* dest, void* src, int count)
		{
			int block;

			block = count >> 3;

			long* pDest = (long*)dest;
			long* pSrc = (long*)src;

			for (int i = 0; i < block; i++)
			{
				*pDest = *pSrc; pDest++; pSrc++;
			}
			dest = pDest;
			src = pSrc;
			count = count - (block << 3);

			if (count > 0)
			{
				byte* pDestB = (byte*)dest;
				byte* pSrcB = (byte*)src;
				for (int i = 0; i < count; i++)
				{
					*pDestB = *pSrcB; pDestB++; pSrcB++;
				}
			}
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void FastCopy(byte[] dstBuffer, int dstOffset, byte[] srcBuffer, int srcOffset, int count)
		{
			if (count < 128)
			{
				unsafe
				{
					fixed (void* pDest = &dstBuffer[dstOffset])
					fixed (void* pSrc = &srcBuffer[srcOffset])
					{
						CustomCopy(pDest, pSrc, count);
					}
				}
			}
			else
			{
				Buffer.BlockCopy(srcBuffer, srcOffset, dstBuffer, dstOffset, count);
			}
		}
	}
}