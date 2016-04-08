namespace RabbitMqNext
{
	using System.IO;

	public struct UndeliveredMessage
	{
		public string exchange;
		public string replyText;
		public ushort replyCode;
		public string routingKey;
		public int bodySize;
		public BasicProperties properties;
		public Stream stream;
	}
}