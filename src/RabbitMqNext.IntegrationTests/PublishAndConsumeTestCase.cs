namespace RabbitMqNext.IntegrationTests
{
	using System;
	using System.Collections.Generic;
	using System.Threading.Tasks;
	using NUnit.Framework;

	[TestFixture]
	public class PublishAndConsumeTestCase : BaseTest
	{
		[Test]
		public async Task PublishAndConsume()
		{
			var conn = await base.StartConnection();
			var channel1 = await conn.CreateChannel();
			channel1.OnError += error =>
			{
				Console.WriteLine("error " + error.ReplyText);
			};

			await channel1.ExchangeDeclare("test_direct", "direct", true, false, null, waitConfirmation: true);
			await channel1.QueueDeclare("queue_direct", false, true, false, false, null, waitConfirmation: true);
			await channel1.QueueBind("queue_direct", "test_direct", "routing", null, waitConfirmation: true);

			var channel2 = await conn.CreateChannel();
			channel2.OnError += error =>
			{
				Console.WriteLine("error " + error.ReplyText);
			};

			var deliveries = new List<MessageDelivery>();

			await channel2.BasicConsume(ConsumeMode.SingleThreaded, delivery =>
			{
				deliveries.Add(delivery);

				return Task.CompletedTask;
			}, "queue_direct", "", withoutAcks: true, exclusive: true, arguments: null, waitConfirmation: true);


			channel1.BasicPublish("test_direct", "routing", true, false, BasicProperties.Empty,
				new ArraySegment<byte>(new byte[] {4, 3, 2, 1, 0}));
		}
	}
}