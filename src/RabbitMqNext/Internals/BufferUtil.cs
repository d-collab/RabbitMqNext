namespace RabbitMqNext.Internals
{
	using System;
	using RingBuffer;

	internal static class BufferUtil
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
	}
}