namespace RabbitMqNext.Internals
{
	using System;

	internal static class FrameParameters
	{
		internal class CloseParams
		{
			public ushort replyCode;
			public string replyText;
		}

		internal class BasicPublishArgs
		{
			public string exchange;
			public string routingKey;
			public bool mandatory;
			public bool immediate;
			public BasicProperties properties;
			public ArraySegment<byte> buffer;
		}

	}
}