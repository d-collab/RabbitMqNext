namespace RecoveryScenariosApp
{
	using System;
	using System.Threading.Tasks;
	using RabbitMqNext;

	class Consumer : IQueueConsumer
	{
		private readonly string _name;

		public Consumer(string name)
		{
			_name = name;
		}

		public Task Consume(MessageDelivery delivery)
		{
			Console.WriteLine("[Consumer " + _name + "] Consume received msg " + delivery.deliveryTag);

			return Task.CompletedTask;
		}

		public void Broken()
		{
			Console.WriteLine("[Consumer " + _name + "] Broken");
		}

		public void Recovered()
		{
			Console.WriteLine("[Consumer " + _name + "] Recovered");
		}

		public void Cancelled()
		{
			Console.WriteLine("[Consumer " + _name + "] Cancelled");
		}
	}
}