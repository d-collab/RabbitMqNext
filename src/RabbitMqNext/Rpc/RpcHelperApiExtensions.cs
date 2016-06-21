namespace RabbitMqNext
{
	using System;
	using System.Threading.Tasks;

	public static class RpcHelperApiExtensions
	{
		public static Task<MessageDelivery> Call(this RpcHelper source, string exchange, string routing, BasicProperties properties, byte[] buffer)
		{
			return source.Call(exchange, routing, properties, new ArraySegment<byte>(buffer));
		}
	}
}