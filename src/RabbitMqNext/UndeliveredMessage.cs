namespace RabbitMqNext
{
	using System.IO;

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