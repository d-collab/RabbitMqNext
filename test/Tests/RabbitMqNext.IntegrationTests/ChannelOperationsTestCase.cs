namespace RabbitMqNext.IntegrationTests
{
	using System;
	using System.Collections.Generic;
	using System.Text;
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
		public async Task ExchangeDelete_with_wait_Should_waitconfirmation()
		{
			Console.WriteLine("ExchangeDelete_with_wait_Should_waitconfirmation");

			var conn = await base.StartConnection(AutoRecoverySettings.Off);
			var channel = await conn.CreateChannel();
			channel.AddErrorCallback(error =>
			{
				Console.Error.WriteLine("error " + error.ReplyText);
				return Task.CompletedTask;
			});

			await channel.ExchangeDeclare("test_direct", "direct", true, false, null, waitConfirmation: true);
			await channel.ExchangeDelete("test_direct", waitConfirmation: true);
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

		[Test]
		public async Task Publish_confirmation_stress()
		{
			Console.WriteLine("Publish_confirmation_stress");

			var numberMessages = 2000;
			var chunkSize = 1000;
			var chunks = numberMessages / chunkSize;

			var conn = await base.StartConnection(AutoRecoverySettings.Off);
			var channel = await conn.CreateChannelWithPublishConfirmation(maxunconfirmedMessages: 100);

			try
			{
				await channel.ExchangeDeclare("pub_ex", "fanout", true, false, null, true);
				await channel.QueueDeclare("queue_direct10", false, true, false, false, null, true);
				await channel.QueueBind("queue_direct10", "pub_ex", "", null, true);

				for (int i = 0; i < chunks; i++)
				{
					var tasks = new List<Task>(chunkSize);

					for (int j = 0; j < chunkSize; j++)
					{
						var properties = channel.RentBasicProperties();

						var bodyBytes = Encoding.UTF8.GetBytes("This is the message body text");
						var body = new ArraySegment<byte>(bodyBytes);

						var task = channel.BasicPublishWithConfirmation("pub_ex", "", true, properties, body);

						tasks.Add(task);
					}

					await Task.WhenAll(tasks);
				}
			}
			finally
			{
				channel.QueuePurge("queue_direct10", waitConfirmation: false).Wait();
			}
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