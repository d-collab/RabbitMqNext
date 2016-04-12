namespace RabbitMqNext
{
	using System;

	public static class RpcHelperApiExtensions
	{
		public static TaskSlim<MessageDelivery> Call(this RpcHelper source, string exchange, string routing, BasicProperties properties, byte[] buffer)
		{
			return source.Call(exchange, routing, properties, new ArraySegment<byte>(buffer));
		}
	}
}