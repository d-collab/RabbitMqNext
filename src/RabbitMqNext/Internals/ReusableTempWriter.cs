namespace RabbitMqNext.Internals
{
	using System;
	using System.Buffers;

	internal class ReusableTempWriter : IDisposable
	{
		internal MemoryStreamSlim _memoryStream;
		internal InternalBigEndianWriter _innerWriter;
		internal AmqpPrimitivesWriter _writer2;

		public ReusableTempWriter(ArrayPool<byte> bufferPool, ObjectPoolArray<ReusableTempWriter> memStreamPool)
		{
			_memoryStream = new MemoryStreamSlim(bufferPool, AmqpPrimitivesWriter.BufferSize);

			_innerWriter = new InternalBigEndianWriter(_memoryStream);

			_writer2 = new AmqpPrimitivesWriter(bufferPool, memStreamPool);
			_writer2.Initialize(_innerWriter);
		}

		public void EnsureMaxFrameSizeSet(uint? frameMax)
		{
			_writer2.FrameMaxSize = frameMax;
		}

		public void Dispose()
		{
			_memoryStream.Dispose();
		}
	}
}