namespace RabbitMqNext.Internals
{
	using System;

	internal interface IFrameContentWriter
	{
		void Write(AmqpPrimitivesWriter amqpWriter, ushort channel, ushort classId, ushort methodId, object optionalArg);
	}

	internal static class FrameParameters
	{
		internal class CloseParams
		{
			public ushort replyCode;
			public string replyText;
		}

		internal class BasicAckArgs : IFrameContentWriter
		{
			public ulong deliveryTag;
			public bool multiple;

			public void Write(AmqpPrimitivesWriter amqpWriter, ushort channel, ushort classId, ushort methodId, object optionalArg)
			{
				AmqpChannelLevelFrameWriter.InternalBasicAck(amqpWriter, channel, classId, methodId, optionalArg);
			}
		}

		internal class BasicNAckArgs : IFrameContentWriter
		{
			public ulong deliveryTag;
			public bool multiple;
			public bool requeue;

			public void Write(AmqpPrimitivesWriter amqpWriter, ushort channel, ushort classId, ushort methodId, object optionalArg)
			{
				AmqpChannelLevelFrameWriter.InternalBasicNAck(amqpWriter, channel, classId, methodId, optionalArg);
			}
		}

		internal class BasicPublishArgs : IFrameContentWriter
		{
			private readonly Action<BasicPublishArgs> _recycler;

			public BasicPublishArgs(Action<BasicPublishArgs> recycler)
			{
				_recycler = recycler;
			}

			public string exchange;
			public string routingKey;
			public bool mandatory;
			public bool immediate;
			public BasicProperties properties;
			public ArraySegment<byte> buffer;

			public void Done()
			{
				if (_recycler != null)
					_recycler(this);
			}

			public void Write(AmqpPrimitivesWriter amqpWriter, ushort channel, ushort classId, ushort methodId, object optionalArg)
			{
				// AmqpChannelLevelFrameWriter.InternalBasicPublish(amqpWriter, channel, classId, methodId, optionalArg);
				AmqpChannelLevelFrameWriter.InternalBufferedBasicPublish(amqpWriter, channel, classId, methodId, optionalArg);
			}
		}
	}
}