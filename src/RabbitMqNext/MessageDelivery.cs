namespace RabbitMqNext
{
	using System;
	using System.IO;

	public struct MessageDelivery
	{
		// public string consumerTag;
		// public string exchange;
		public ulong deliveryTag;
		public bool redelivered;
		public string routingKey;
		public int bodySize;
		public BasicProperties properties;
		public Stream stream;
		public byte[] Body;
	}

	public struct UndeliveredMessage
	{
		// public string consumerTag;
		public string exchange;
		public string replyText;
		public ushort replyCode;
//		public ulong deliveryTag;
//		public bool redelivered;
		public string routingKey;
		public int bodySize;
		public BasicProperties properties;
		public Stream stream;
	}
}