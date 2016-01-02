namespace RabbitMqNext.Internals
{
	using System;
	using WriterDelegate = System.Action<AmqpPrimitivesWriter, ushort, ushort, ushort>;

	static class AmqpChannelLevelFrameWriter
	{
		public static WriterDelegate ChannelOpen()
		{
			const uint payloadSize = 4 + 1;

			return (writer, channel, classId, methodId) =>
			{
				Console.WriteLine("ChannelOpen");

				writer.WriteOctet(AmqpConstants.FrameMethod);
				writer.WriteUShort(channel); // channel
				writer.WriteLong(payloadSize); // payload size

				writer.WriteUShort(classId);
				writer.WriteUShort(methodId);
				writer.WriteShortstr("");

				writer.WriteOctet(AmqpConstants.FrameEnd);
			};
		}

		public static WriterDelegate ExchangeDeclare()
		{
			return (writer, channel, classId, methodId) =>
			{

			};
		}

		public static WriterDelegate BasicPublish()
		{
			return (writer, channel, classId, methodId) =>
			{

			};
		}
	}
}