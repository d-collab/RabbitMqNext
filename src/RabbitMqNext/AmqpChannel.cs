namespace RabbitMqNext
{
	using System;
	using System.Threading.Tasks;

	public class AmqpChannel
	{
		public AmqpChannel(int channelNum)
		{
		}

		public Task<bool> BasicQos()
		{
			var tcs = new TaskCompletionSource<bool>();
			return tcs.Task;
		}

		public Task<bool> ExchangeDeclare()
		{
			var tcs = new TaskCompletionSource<bool>();
			return tcs.Task;
		}

		public Task BasicPublish(string exchange, string routingKey, bool mandatory, bool immediate, 
								 BasicProperties properties, ArraySegment<byte> buffer)
		{
			var tcs = new TaskCompletionSource<bool>();

			return tcs.Task;
		}
	}
}