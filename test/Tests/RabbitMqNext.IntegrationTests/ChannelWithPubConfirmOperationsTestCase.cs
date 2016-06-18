namespace RabbitMqNext.IntegrationTests
{
	using System;
	using System.Collections.Generic;
	using System.Threading.Tasks;
	using FluentAssertions;
	using NUnit.Framework;

	[TestFixture]
	public class ChannelWithPubConfirmOperationsTestCase : BaseTest
	{
		[Test]
		public async Task Parallel_Consumer_Publish()
		{
			Console.WriteLine("Parallel_Consumer_Publish");

			var conn = await base.StartConnection(AutoRecoverySettings.Off);
			
			// !!!
			var channel1 = await conn.CreateChannelWithPublishConfirmation();
			channel1.AddErrorCallback(error =>
			{
				Console.Error.WriteLine("error " + error.ReplyText);
				return Task.CompletedTask;
			});

			await channel1.ExchangeDeclare("test_direct_conf", "direct", true, false, null, waitConfirmation: true);
			await channel1.QueueDeclare("queue_direct_conf", false, true, false, false, null, waitConfirmation: true);
			await channel1.QueueBind("queue_direct_conf", "test_direct_conf", "routing", null, waitConfirmation: true);

			var channel2 = await conn.CreateChannel();
			channel2.AddErrorCallback(error =>
			{
				Console.Error.WriteLine("error " + error.ReplyText);
				return Task.CompletedTask;
			});

			var deliveries = new List<MessageDelivery>();

			await channel2.BasicConsume(ConsumeMode.ParallelWithBufferCopy, delivery =>
			{
				lock (deliveries)
					deliveries.Add(delivery);

				delivery.bodySize.Should().Be(5);
				var buffer = new byte[5];
				delivery.stream.Read(buffer, 0, 5).Should().Be(5);
				buffer.Should().BeEquivalentTo(new byte[] { 4, 3, 2, 1, 0 });

				return Task.CompletedTask;
			}, "queue_direct_conf", consumerTag: "", withoutAcks: true, exclusive: true, arguments: null, waitConfirmation: true);

			await channel1.BasicPublishWithConfirmation("test_direct_conf", "routing", true, BasicProperties.Empty, new ArraySegment<byte>(new byte[] { 4, 3, 2, 1, 0 }));
			await channel1.BasicPublishWithConfirmation("test_direct_conf", "routing", true, BasicProperties.Empty, new ArraySegment<byte>(new byte[] { 4, 3, 2, 1, 0 }));
			await channel1.BasicPublishWithConfirmation("test_direct_conf", "routing", true, BasicProperties.Empty, new ArraySegment<byte>(new byte[] { 4, 3, 2, 1, 0 }));
			await channel1.BasicPublishWithConfirmation("test_direct_conf", "routing", true, BasicProperties.Empty, new ArraySegment<byte>(new byte[] { 4, 3, 2, 1, 0 }));
			await Task.Delay(1500);

			deliveries.Should().HaveCount(4);

			var delivery1 = deliveries[0];
			delivery1.deliveryTag.Should().Be(1);
			delivery1.redelivered.Should().BeFalse();
			delivery1.routingKey.Should().Be("routing");
		}

	}
}