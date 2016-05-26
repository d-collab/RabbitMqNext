namespace RabbitMqNext.IntegrationTests
{
	using System;
	using System.Collections.Concurrent;
	using System.Collections.Generic;
	using System.Linq;
	using System.Threading;
	using System.Threading.Tasks;
	using NUnit.Framework;

	[TestFixture, Explicit]
	public class StressMultiThreadedTestCase : BaseTest
	{
		private static Random _rnd = new Random();

		private readonly ConcurrentDictionary<string, bool> _queues;

		public StressMultiThreadedTestCase()
		{
			_queues = new ConcurrentDictionary<string, bool>();
		}

		[Test]
		public async Task BasicRpcWithSingleReply()
		{
			var conn = await base.StartConnection(AutoRecoverySettings.Off);
			var channel = await conn.CreateChannel();

			var cancelToken = new CancellationTokenSource();

			PublishSomeMessage(channel, cancelToken);

			var tasks = new List<Task>();
			for (int i = 0; i < 100; i++)
			{
				var task = Task.Factory.StartNew(() => CreateTempQueueAndConsume(channel)).Unwrap();
				tasks.Add(task);
			}

			await Task.Delay(1000); // trick to move out of readframe thread

			Task.WaitAll(tasks.ToArray());

			cancelToken.Cancel();

			conn.Dispose();
		}

		private void PublishSomeMessage(IChannel channel, CancellationTokenSource cancelToken)
		{
			Task.Factory.StartNew(() =>
			{
				var msgIndex = 0;

				while (!cancelToken.IsCancellationRequested)
				{
					Thread.Sleep(1);

					var index = _rnd.Next(_queues.Count);
					string queue = null;
					try
					{
						queue = _queues.Keys.ElementAt(index);
					}
					catch (Exception)
					{
						continue;
					}

					// Sends
					channel.BasicPublishFast("", queue, false, BasicProperties.Empty, BitConverter.GetBytes(msgIndex++));
				}

			}, TaskCreationOptions.LongRunning);
		}

		private async Task CreateTempQueueAndConsume(IChannel channel)
		{
			// declare temp queue
			var queueName = await channel.QueueDeclare("", passive: false, 
				durable: false, exclusive: true, autoDelete: true, arguments: null,
				waitConfirmation: true);

			_queues[queueName.Name] = true;
//
//			Console.WriteLine("Declared " + queueName);

			// Consume
			var consumerTag = await channel.BasicConsume(ConsumeMode.SingleThreaded, delivery =>
			{
				// Console.WriteLine("delivery " + delivery.deliveryTag + " " + queueName.Name);

				return Task.CompletedTask;

			}, queueName.Name, null, true, false, null, true);

			// give it some time
			await Task.Delay(10000);

			// Cancel consume
			_queues[queueName.Name] = false;
			bool val;
			_queues.TryRemove(queueName.Name, out val);

			await channel.BasicCancel(consumerTag, true);
//
//			Console.WriteLine("Canceled for " + queueName);
		}
	}
}