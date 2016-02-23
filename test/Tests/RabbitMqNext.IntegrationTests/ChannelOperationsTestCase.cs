namespace RabbitMqNext.IntegrationTests
{
	using System;
	using System.Threading;
	using System.Threading.Tasks;
	using FluentAssertions;
	using Internals;
	using NUnit.Framework;


	[TestFixture]
	public class ChannelOperationsTestCase : BaseTest
	{
		[Test]
		public async Task BasicQos()
		{
			using (var conn = await base.StartConnection())
			using (var channel = await conn.CreateChannel())
			{
				await channel.BasicQos(0, 250, true);

				// need to send another command which expects for reply
				await channel.ExchangeDeclare("test_ex", "direct", true, false, null, true);
			}
		}

		[Test]
		public async Task BasicAck()
		{
			var conn = await base.StartConnection();
			var channel = await conn.CreateChannel();
			channel.OnError += error =>
			{
				Console.WriteLine("error " + error.ReplyText);
				error.ReplyText.Should().Be("PRECONDITION_FAILED - unknown delivery tag 2");
			};

			channel.BasicAck(2, true); // will cause the channel to close, since its invalid
		}

		[Test]
		public async Task BasicNAck()
		{
			var conn = await base.StartConnection();
			var channel = await conn.CreateChannel();
			channel.OnError += error =>
			{
				Console.WriteLine("error " + error.ReplyText);
				error.ReplyText.Should().Be("PRECONDITION_FAILED - unknown delivery tag 2");
			};

			channel.BasicNAck(2, false, requeue: true); // will cause the channel to close, since its invalid
		}

		[Test]
		public async Task QueueDeclare_with_wait_Should_return_queueInfo()
		{
			var conn = await base.StartConnection();
			var channel = await conn.CreateChannel();
			channel.OnError += error =>
			{
				Console.WriteLine("error " + error.ReplyText);
			};

			var queueInfo = await channel.QueueDeclare("queue1", false, true, false, false, null, waitConfirmation: true);
			queueInfo.Should().NotBeNull();
			queueInfo.Name.Should().Be("queue1");
		}

		[Test]
		public async Task ExchangeDeclare_with_wait_Should_waitconfirmation()
		{
			var conn = await base.StartConnection();
			var channel = await conn.CreateChannel();
			channel.OnError += error =>
			{
				Console.WriteLine("error " + error.ReplyText);
			};

			await channel.ExchangeDeclare("test_direct", "direct", true, false, null, waitConfirmation: true);
			await channel.ExchangeDeclare("test_fanout", "fanout", true, false, null, waitConfirmation: true);
			await channel.ExchangeDeclare("test_topic", "topic", true, false, null, waitConfirmation: true);
		}

		[Test]
		public async Task QueueBind_should_await_confirmation()
		{
			var conn = await base.StartConnection();
			var channel = await conn.CreateChannel();
			channel.OnError += error =>
			{
				Console.WriteLine("error " + error.ReplyText);
			};

			await channel.ExchangeDeclare("test_direct", "direct", true, false, null, waitConfirmation: true);
			await channel.QueueDeclare("queue_direct", false, true, false, false, null, waitConfirmation: true);

			await channel.QueueBind("queue_direct", "test_direct", "routing", null, waitConfirmation: true);
		}

		[Test, ExpectedException(ExpectedMessage = "Error: Server returned error: COMMAND_INVALID - unknown exchange type 'SOMETHING' [code: 503 class: 40 method: 10]")]
		public async Task InvalidCommand_Should_SignalExceptionOnTask()
		{
			using (var conn = await base.StartConnection())
			{
				var channel = await conn.CreateChannel();

				await channel.ExchangeDeclare("", "SOMETHING", durable: false, autoDelete: false, arguments: null, waitConfirmation: true);
			}
		}
	}
}