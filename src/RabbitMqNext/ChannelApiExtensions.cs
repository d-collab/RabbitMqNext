namespace RabbitMqNext
{
	using System;

	public static class ChannelApiExtensions
	{
		public static TaskSlim BasicPublish(this Channel source, 
			string exchange, string routingKey, bool mandatory, bool immediate,
			BasicProperties properties, byte[] buffer)
		{
			return source.BasicPublish(exchange, routingKey, mandatory, immediate, properties, new ArraySegment<byte>(buffer));
		}

		public static TaskSlim BasicPublish(this Channel source,
			string exchange, string routingKey, bool mandatory,
			BasicProperties properties, byte[] buffer)
		{
			return source.BasicPublish(exchange, routingKey, mandatory, false, properties, new ArraySegment<byte>(buffer));
		}

		public static void BasicPublishFast(this Channel source,
			string exchange, string routingKey, bool mandatory, bool immediate,
			BasicProperties properties, byte[] buffer)
		{
			source.BasicPublishFast(exchange, routingKey, mandatory, immediate, properties, new ArraySegment<byte>(buffer));
		}

		public static void BasicPublishFast(this Channel source,
			string exchange, string routingKey, bool mandatory,
			BasicProperties properties, byte[] buffer)
		{
			source.BasicPublishFast(exchange, routingKey, mandatory, false, properties, new ArraySegment<byte>(buffer));
		}
	}
}