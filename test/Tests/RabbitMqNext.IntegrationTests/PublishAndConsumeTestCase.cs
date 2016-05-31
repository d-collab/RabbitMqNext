namespace RabbitMqNext.IntegrationTests
{
	using System;
	using System.Collections.Generic;
	using System.Threading;
	using System.Threading.Tasks;
	using FluentAssertions;
	using NUnit.Framework;

	[TestFixture]
	public class PublishAndConsumeTestCase : BaseTest
	{
		[Test]
		public async Task UnroutedMessage_TriggerEvents()
		{
			Console.WriteLine("UnroutedMessage_TriggerEvents");

			var conn = await base.StartConnection(AutoRecoverySettings.Off);
			var channel = await conn.CreateChannel();

			UndeliveredMessage undelivered = default(UndeliveredMessage);

			channel.MessageUndeliveredHandler += message =>
			{
				undelivered = message;
				return Task.CompletedTask;
			};

			await channel.ExchangeDeclare("test_direct", "direct", true, false, null, waitConfirmation: true);
			
			channel.BasicPublishFast("test_direct", "non_existing_routing", mandatory: true, properties: null, buffer: new ArraySegment<byte>(new byte[] { 1,2,3 }));

			await Task.Delay(500);

			undelivered.exchange.Should().Be("test_direct");
			undelivered.bodySize.Should().Be(3);
			undelivered.replyCode.Should().Be(312);
			undelivered.replyText.Should().Be("NO_ROUTE");
			undelivered.routingKey.Should().Be("non_existing_routing");
		}

		[Test]
		public async Task Parallel_Consumer_FastPublish()
		{
			Console.WriteLine("Parallel_Consumer_FastPublish");


			var conn = await base.StartConnection(AutoRecoverySettings.Off);
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

			await channel2.BasicConsume(ConsumeMode.ParallelWithBufferCopy, delivery =>
			{
				deliveries.Add(delivery);

				delivery.bodySize.Should().Be(5);
				var buffer = new byte[5];
				delivery.stream.Read(buffer, 0, 5).Should().Be(5);
				buffer.Should().BeEquivalentTo(new byte[] { 4, 3, 2, 1, 0 });

				return Task.CompletedTask;
			}, "queue_direct", consumerTag: "", withoutAcks: true, exclusive: true, arguments: null, waitConfirmation: true);


			channel1.BasicPublishFast("test_direct", "routing", true, BasicProperties.Empty, new ArraySegment<byte>(new byte[] { 4, 3, 2, 1, 0 }));
			await Task.Delay(1000);

			deliveries.Should().HaveCount(1);

			var delivery1 = deliveries[0];
			delivery1.deliveryTag.Should().Be(1);
			delivery1.redelivered.Should().BeFalse();
			delivery1.routingKey.Should().Be("routing");
		}

		[Test]
		public async Task SingleThreaded_Consumer_AwaitedPublish()
		{
			Console.WriteLine("SingleThreaded_Consumer_AwaitedPublish");


			var conn = await base.StartConnection(AutoRecoverySettings.Off);
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

				delivery.bodySize.Should().Be(5);
				var buffer = new byte[5];
				delivery.stream.Read(buffer, 0, 5).Should().Be(5);
				buffer.Should().BeEquivalentTo(new byte[] { 4, 3, 2, 1, 0 });

				return Task.CompletedTask;
			}, "queue_direct", consumerTag: "", withoutAcks: true, exclusive: true, arguments: null, waitConfirmation: true);


			channel1.BasicPublishFast("test_direct", "routing", true, BasicProperties.Empty, new ArraySegment<byte>(new byte[] { 4, 3, 2, 1, 0 }));
			Console.WriteLine("BasicPublish done");

			await Task.Delay(1000);

			deliveries.Should().HaveCount(1);
			
			var delivery1 = deliveries[0];
			delivery1.deliveryTag.Should().Be(1);
			delivery1.redelivered.Should().BeFalse();
			delivery1.routingKey.Should().Be("routing");
		}

		[Test]
		public async Task SerializedWithCopy_Consumer_FastPublish()
		{
			Console.WriteLine("SerializedWithCopy_Consumer_FastPublish");

			var conn = await base.StartConnection(AutoRecoverySettings.Off);
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

			var deliveries = new List<int>();

			await channel2.BasicConsume(ConsumeMode.SerializedWithBufferCopy, delivery =>
			{
				var buffer = new byte[4];
				delivery.stream.Read(buffer, 0, 4);

				deliveries.Add(BitConverter.ToInt32(buffer, 0));

				return Task.CompletedTask;

			}, "queue_direct", consumerTag: "", withoutAcks: true, exclusive: true, arguments: null, waitConfirmation: true);

			await Task.Delay(200);

			// Publishs a bunch of messages
			var count = 1000;
			for (int i = 0; i < count; i++)
			{
				channel1.BasicPublishFast("test_direct", "routing", true, BasicProperties.Empty, BitConverter.GetBytes(i));
			}

			await Task.Delay(3500);

			// Confirms we got all of them
			deliveries.Should().HaveCount(count);

			// Confirms ordering
			for (int i = 0; i < count; i++)
			{
				deliveries[i].Should().Be(i);
			}
		}
	}
}