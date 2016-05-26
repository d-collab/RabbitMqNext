namespace RabbitMqNext.IntegrationTests
{
	using System;
	using System.IO;
	using System.Linq;
	using System.Text;
	using System.Threading.Tasks;
	using FluentAssertions;
	using NUnit.Framework;

	[TestFixture]
	public class RpcHelperTestCase : BaseTest
	{
		[Test]
		public async Task BasicRpcWithSingleReply()
		{
			Console.WriteLine("BasicRpcWithSingleReply");

			using (var conn1 = await base.StartConnection(AutoRecoverySettings.Off))
			{
				var conn2 = await base.StartConnection(AutoRecoverySettings.Off);
				var channelWorker = await conn1.CreateChannel();
				channelWorker.OnError += error =>
				{
					Console.WriteLine("error " + error.ReplyText);
				};

				await channelWorker.QueueDeclare("queue_rpc1", false, false, false, true, null, waitConfirmation: true);

				await channelWorker.BasicConsume(ConsumeMode.SingleThreaded, (delivery =>
				{
					var replyProp = channelWorker.RentBasicProperties();
					replyProp.CorrelationId = delivery.properties.CorrelationId;

					var buffer = new byte[delivery.bodySize];
					delivery.stream.Read(buffer, 0, delivery.bodySize);

					// Just echo the input back to the originator
					channelWorker.BasicPublishFast("", delivery.properties.ReplyTo, false, replyProp, buffer);

					return Task.CompletedTask;

				}), "queue_rpc1", null, withoutAcks: true, exclusive: true, arguments: null, waitConfirmation: true);

				using (conn2)
				{
					var channelSender = await conn2.CreateChannel();
					channelSender.OnError += error =>
					{
						Console.WriteLine("error " + error.ReplyText);
					};

					var rpcHelper = await channelSender.CreateRpcHelper(ConsumeMode.SingleThreaded, timeoutInMs: 1000);

					var reply1 = await rpcHelper.Call("", "queue_rpc1", null, new ArraySegment<byte>(Encoding.UTF8.GetBytes("hello world")));

					var replyBuffer = new byte[reply1.bodySize];
					reply1.stream.Read(replyBuffer, 0, replyBuffer.Length);
					var replyTxt = Encoding.UTF8.GetString(replyBuffer);
					Console.WriteLine("reply is " + replyTxt);

					replyTxt.Should().Be("hello world");
				}	
			}
		}

		[Test, Ignore("doesnt work on appveyor for some reason")]
		public async Task BasicRpcWithMultipleReplies()
		{
			Console.WriteLine("BasicRpcWithMultipleReplies");

			using (var conn1 = await base.StartConnection(AutoRecoverySettings.Off))
			{
				var conn2 = await base.StartConnection(AutoRecoverySettings.Off);
				var channelWorker = await conn1.CreateChannel();
				channelWorker.OnError += error =>
				{
					Console.WriteLine("error " + error.ReplyText);
				};

				await channelWorker.ExchangeDeclare("rpc_exchange1", "fanout", true, false,null, waitConfirmation: true);
				await channelWorker.QueueDeclare("queue_rpc_fan1", false, false, false, true, null, waitConfirmation: true);
				await channelWorker.QueueDeclare("queue_rpc_fan2", false, false, false, true, null, waitConfirmation: true);

				await channelWorker.QueueBind("queue_rpc_fan1", "rpc_exchange1", "", null, waitConfirmation: true);
				await channelWorker.QueueBind("queue_rpc_fan2", "rpc_exchange1", "", null, waitConfirmation: true);

				await channelWorker.BasicConsume(ConsumeMode.SingleThreaded, (delivery =>
				{
					var replyProp = channelWorker.RentBasicProperties();
					replyProp.CorrelationId = delivery.properties.CorrelationId;

					channelWorker.BasicPublishFast("", delivery.properties.ReplyTo, false, replyProp, Encoding.UTF8.GetBytes("reply1"));

					return Task.CompletedTask;

				}), "queue_rpc_fan1", null, withoutAcks: true, exclusive: true, arguments: null, waitConfirmation: true);

				await channelWorker.BasicConsume(ConsumeMode.SingleThreaded, (delivery =>
				{
					var replyProp = channelWorker.RentBasicProperties();
					replyProp.CorrelationId = delivery.properties.CorrelationId;

					channelWorker.BasicPublishFast("", delivery.properties.ReplyTo, false, replyProp, Encoding.UTF8.GetBytes("reply2"));

					return Task.CompletedTask;

				}), "queue_rpc_fan2", null, withoutAcks: true, exclusive: true, arguments: null, waitConfirmation: true);


				using (conn2)
				{
					var channelSender = await conn2.CreateChannel();

					var rpcHelper = await channelSender.CreateRpcAggregateHelper(ConsumeMode.SingleThreaded, timeoutInMs: 30000);

					var replies = await rpcHelper.CallAggregate("rpc_exchange1", "", null, 
						new ArraySegment<byte>(Encoding.UTF8.GetBytes("hello world")), 
						minExpectedReplies: 2);

					replies.Should().NotBeNull();
					replies.Should().HaveCount(2);
					var repliesAsStrings = 
						replies
						.Select(rep => Tuple.Create(rep.stream, new byte[rep.bodySize]))
						.Select(tuple => { tuple.Item1.Read(tuple.Item2, 0, tuple.Item2.Length);
							                 return tuple.Item2;
						})
						.Select(Encoding.UTF8.GetString)
						.ToArray();

					Console.WriteLine("replies are " + string.Join(", ", repliesAsStrings));

					repliesAsStrings.Should().Contain("reply1").And.Contain("reply2");
				}
			}
		}

		[Test]
		public async Task BasicRpcWithSingleReplyAndReallyLargeBody()
		{
			Console.WriteLine("BasicRpcWithSingleReplyAndReallyLargeBody");

			var charBuffer = new char[131072 + 12];
			for (int i = 0; i < charBuffer.Length; i++)
				charBuffer[i] = Convert.ToChar( 'a' + (i % 26) );

			for (int i = charBuffer.Length - 1; i > charBuffer.Length - 14; i--)
				charBuffer[i] = Convert.ToChar('0' + (i % 10));

			var content = new String(charBuffer);

			using (var conn1 = await base.StartConnection(AutoRecoverySettings.Off))
			{
				var conn2 = await base.StartConnection(AutoRecoverySettings.Off);
				var channelWorker = await conn1.CreateChannel();
				channelWorker.OnError += error =>
				{
					Console.WriteLine("error " + error.ReplyText);
				};

				await channelWorker.QueueDeclare("queue_rpc3", false, false, false, true, null, waitConfirmation: true);

				await channelWorker.BasicConsume(ConsumeMode.SingleThreaded, (delivery =>
				{
					var replyProp = channelWorker.RentBasicProperties();
					replyProp.CorrelationId = delivery.properties.CorrelationId;

					var buffer = Encoding.UTF8.GetBytes(content);

					channelWorker.BasicPublishFast("", delivery.properties.ReplyTo, false, replyProp, buffer);

					return Task.CompletedTask;

				}), "queue_rpc3", null, withoutAcks: true, exclusive: true, arguments: null, waitConfirmation: true);

				using (conn2)
				{
					var channelSender = await conn2.CreateChannel();
					channelSender.OnError += error =>
					{
						Console.WriteLine("error " + error.ReplyText);
					};

					var rpcHelper = await channelSender.CreateRpcHelper(ConsumeMode.SingleThreaded, timeoutInMs: 30000);

					var reply1 = await rpcHelper.Call("", "queue_rpc3", null, new ArraySegment<byte>(Encoding.UTF8.GetBytes("hello world")));

					reply1.bodySize.Should().Be(content.Length);

					var replyBuffer = new byte[reply1.bodySize];
					var read = reply1.stream.Read(replyBuffer, 0, replyBuffer.Length);
					if (read < reply1.bodySize)
						reply1.stream.Read(replyBuffer, read, replyBuffer.Length - read);

					var replyTxt = Encoding.UTF8.GetString(replyBuffer);
					replyTxt.Should().Be(content);
				}
			}
		}
	}
}