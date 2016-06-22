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
			Console.WriteLine("BasicQos");

			using (var conn = await base.StartConnection(AutoRecoverySettings.Off))
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
			Console.WriteLine("BasicAck");

			var conn = await base.StartConnection(AutoRecoverySettings.Off);
			var channel = await conn.CreateChannel();
			channel.AddErrorCallback(error =>
			{
				Console.Error.WriteLine("error " + error.ReplyText);
				error.ReplyText.Should().Be("PRECONDITION_FAILED - unknown delivery tag 2");
				return Task.CompletedTask;
			});

			channel.BasicAck(2, true); // will cause the channel to close, since its invalid
		}

		[Test]
		public async Task BasicNAck()
		{
			Console.WriteLine("BasicNAck");

			var conn = await base.StartConnection(AutoRecoverySettings.Off);
			var channel = await conn.CreateChannel();
			channel.AddErrorCallback(error =>
			{
				Console.Error.WriteLine("error " + error.ReplyText);
				error.ReplyText.Should().Be("PRECONDITION_FAILED - unknown delivery tag 2");
				return Task.CompletedTask;
			});

			channel.BasicNAck(2, false, requeue: true); // will cause the channel to close, since its invalid
		}

		[Test]
		public async Task QueueDeclare_with_wait_Should_return_queueInfo()
		{
			Console.WriteLine("QueueDeclare_with_wait_Should_return_queueInfo");

			var conn = await base.StartConnection(AutoRecoverySettings.Off);
			var channel = await conn.CreateChannel();
			channel.AddErrorCallback(error =>
			{
				Console.Error.WriteLine("error " + error.ReplyText);
				return Task.CompletedTask;
			});

			var queueInfo = await channel.QueueDeclare("queue1", false, true, false, false, null, waitConfirmation: true);
			queueInfo.Should().NotBeNull();
			queueInfo.Name.Should().Be("queue1");
		}

		[Test]
		public async Task ExchangeDeclare_with_wait_Should_waitconfirmation()
		{
			Console.WriteLine("ExchangeDeclare_with_wait_Should_waitconfirmation");

			var conn = await base.StartConnection(AutoRecoverySettings.Off);
			var channel = await conn.CreateChannel();
			channel.AddErrorCallback(error =>
			{
				Console.Error.WriteLine("error " + error.ReplyText);
				return Task.CompletedTask;
			});

			await channel.ExchangeDeclare("test_direct", "direct", true, false, null, waitConfirmation: true);
			await channel.ExchangeDeclare("test_fanout", "fanout", true, false, null, waitConfirmation: true);
			await channel.ExchangeDeclare("test_topic", "topic", true, false, null, waitConfirmation: true);
		}

		[Test]
		public async Task QueueBind_should_await_confirmation()
		{
			Console.WriteLine("QueueBind_should_await_confirmation");

			var conn = await base.StartConnection(AutoRecoverySettings.Off);
			var channel = await conn.CreateChannel();
			channel.AddErrorCallback(error =>
			{
				Console.Error.WriteLine("error " + error.ReplyText);
				return Task.CompletedTask;
			});

			await channel.ExchangeDeclare("test_direct", "direct", true, false, null, waitConfirmation: true);
			await channel.QueueDeclare("queue_direct", false, true, false, false, null, waitConfirmation: true);

			await channel.QueueBind("queue_direct", "test_direct", "routing", null, waitConfirmation: true);
		}

		[Test]
		public async Task More_than_single_consume_on_exclusive_queue_should_throw_nice_error()
		{
			Console.WriteLine("More_than_single_consume_on_exclusive_queue_should_throw_nice_error");

			var conn = await base.StartConnection(AutoRecoverySettings.Off);
			var channel1 = await conn.CreateChannel();
			channel1.AddErrorCallback(error =>
			{
				Console.Error.WriteLine("error " + error.ReplyText);
				return Task.CompletedTask;
			});
			var channel2 = await conn.CreateChannel();
			channel2.AddErrorCallback(error =>
			{
				Console.Error.WriteLine("error " + error.ReplyText);
				return Task.CompletedTask;
			});

			await channel1.QueueDeclare("queue_direct", false, true, false, false, null, waitConfirmation: true);

			await channel1.BasicConsume(ConsumeMode.ParallelWithBufferCopy, (delivery) => { return Task.CompletedTask; },
				"queue_direct", "", true, exclusive: true, arguments: null, waitConfirmation: true);

			Assert.Throws<AggregateException>(() =>
			{
				var res = channel2.BasicConsume(ConsumeMode.ParallelWithBufferCopy, (delivery) => { return Task.CompletedTask; },
					"queue_direct", "", true, exclusive: true, arguments: null, waitConfirmation: true).Result;
			})
			.InnerExceptions.Should()
							.Contain(e => e.Message == "Error: Server returned error: ACCESS_REFUSED - queue 'queue_direct' in vhost 'clear_test' in exclusive use [code: 403 class: 60 method: 20]");
		}

//		[Explicit, Test,  ExpectedException(ExpectedMessage = "Error: Server returned error: COMMAND_INVALID - unknown exchange type 'SOMETHING' [code: 503 class: 40 method: 10]")]
//		public async Task InvalidCommand_Should_SignalExceptionOnTask()
//		{
//			Console.WriteLine(System.Reflection.MethodBase.GetCurrentMethod().Name);
//
//			using (var conn = await base.StartConnection())
//			{
//				var channel = await conn.CreateChannel();
//
//				await channel.ExchangeDeclare("", "SOMETHING", durable: false, autoDelete: false, arguments: null, waitConfirmation: true);
//			}
//		}
	}
}