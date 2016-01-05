namespace PerfTest
{
	using System;
	using System.Collections.Generic;
	using System.Diagnostics;
	using System.Runtime;
	using System.Text;
	using System.Threading;
	using System.Threading.Tasks;
	using RabbitMqNext;
	using RabbitMQ.Client;
	using RabbitMQ.Client.Events;
	using ConnectionFactory = RabbitMqNext.ConnectionFactory;

	public class Program
	{
		const int TotalPublish = 250000;
//		const int TotalPublish = 10;
//		const int TotalPublish = 100000;

		private static byte[] MessageContent =
			Encoding.UTF8.GetBytes(
				"The Completed event provides a way for client applications to " +
				"complete an asynchronous socket operation. An event handler should " +
				"be attached to the event within a SocketAsyncEventArgs instance when " +
				"an asynchronous socket operation is initiated, otherwise the application " +
				"will not be able to determine when the operation completes.");

	    public static void Main()
	    {
			Console.WriteLine("Is Server GC: " + GCSettings.IsServerGC);
			GCSettings.LargeObjectHeapCompactionMode = GCLargeObjectHeapCompactionMode.CompactOnce;
			Console.WriteLine("Compaction mode: " + GCSettings.LargeObjectHeapCompactionMode);
			Console.WriteLine("Latency mode: " + GCSettings.LatencyMode);
			GCSettings.LatencyMode = GCLatencyMode.SustainedLowLatency;
			Console.WriteLine("New Latency mode: " + GCSettings.LatencyMode);
			
			var t = Start(); // StartOriginalClient(); // Start();
		    t.Wait();

			Console.WriteLine("All done");

		    Thread.CurrentThread.Join();
	    }

		private static async Task Start()
		{
			var conn = await new ConnectionFactory().Connect("localhost", vhost: "clear_test");

			try
			{
				Console.WriteLine("[Connected]");

				var newChannel = await conn.CreateChannel();
				Console.WriteLine("[channel created] " + newChannel.ChannelNumber);
				await newChannel.BasicQos(0, 150, false);

				await newChannel.ExchangeDeclare("test_ex", "direct", true, false, null, true);

				var qInfo = await newChannel.QueueDeclare("queue1", false, true, false, false, null, true);

				Console.WriteLine("[qInfo] " + qInfo);

				await newChannel.QueueBind("queue1", "test_ex", "routing1", null, true);

				var prop = new BasicProperties()
				{
					Type = "type1",
					// DeliveryMode = 2,
					Headers = new Dictionary<string, object> {{"serialization", 0}}
				};

				var watch = Stopwatch.StartNew();
				for (int i = 0; i < TotalPublish; i++)
				{
					prop.Headers["serialization"] = i;

					// var buffer = Encoding.UTF8.GetBytes("Hello world");
					await newChannel.BasicPublish("test_ex", "routing1", false, false, prop, new ArraySegment<byte>(MessageContent));
				}
				watch.Stop();

				Console.WriteLine("BasicPublish stress. Took " + watch.Elapsed.TotalMilliseconds + "ms");



				var newChannel2 = await conn.CreateChannel();
				Console.WriteLine("[channel created] " + newChannel2.ChannelNumber);
				await newChannel2.BasicQos(0, 150, false);


				var temp = new byte[1000000];

				watch = Stopwatch.StartNew();
				int totalReceived = 0;

				var sub = await newChannel2.BasicConsume((properties, stream, len) =>
				{
					stream.Read(temp, 0, len);

					var str = Encoding.UTF8.GetString(temp, 0, len);
					// Console.WriteLine("Received : " + str);

					// newChannel2.BasicAck()

					if (++totalReceived == TotalPublish)
					{
						watch.Stop();
						Console.WriteLine("Consume stress. Took " + watch.Elapsed.TotalMilliseconds + "ms");
						totalReceived = 0;
					}

				}, "queue1", "tag123", true, false, null, true);

				Console.WriteLine("[subscribed to queue] " + sub);

				await Task.Delay(TimeSpan.FromMinutes(5));

				await newChannel.Close();
				await newChannel2.Close();
			}
			catch (AggregateException ex)
			{
				Console.WriteLine("[Captured error] " + ex.Flatten().Message);
			}
			catch (Exception ex)
			{
				Console.WriteLine("[Captured error 2] " + ex.Message);
			}

			await conn.Close();
		}

		private static async Task StartOriginalClient()
		{
			var conn = new RabbitMQ.Client.ConnectionFactory()
			{
				HostName = "localhost",
				VirtualHost = "clear_test",
				UserName = "guest",
				Password = "guest"
			}.CreateConnection();

			var channel = conn.CreateModel();
			channel.BasicQos(0, 150, false);

			channel.ExchangeDeclare("test_ex", "direct", true, false, null);

			channel.QueueDeclare("queue1", true, false, false, null);

			channel.QueueBind("queue1", "test_ex", "routing2", null);

			var prop = channel.CreateBasicProperties();
			prop.Type = "type1";
			// DeliveryMode = 2,
			prop.Headers = new Dictionary<string, object> { { "serialization", 0 } };

			var watch = Stopwatch.StartNew();
			for (int i = 0; i < TotalPublish; i++)
			{
//				prop.Headers["serialization"] = i;
				channel.BasicPublish("test_ex", "routing2", false, prop, MessageContent);
			}
			watch.Stop();
			Console.WriteLine("Standard BasicPublish stress. Took " + watch.Elapsed.TotalMilliseconds + "ms");



			var totalReceived = 0;
			watch = Stopwatch.StartNew();
			channel.BasicConsume("queue1", true, new OldStyleConsumer(body =>
			{
				if (++totalReceived == TotalPublish)
				{
					watch.Stop();
					Console.WriteLine("Consume stress. Took " + watch.Elapsed.TotalMilliseconds + "ms");
					totalReceived = 0;
				}
			}));

			await Task.Delay(TimeSpan.FromSeconds(30));
			// Thread.CurrentThread.Join(TimeSpan.FromSeconds(30));
		}

		class OldStyleConsumer : IBasicConsumer
		{
			private readonly Action<byte[]> _action;

			public OldStyleConsumer(Action<byte[]> action)
			{
				_action = action;
			}

			public void HandleBasicCancel(string consumerTag)
			{
			}

			public void HandleBasicCancelOk(string consumerTag)
			{
			}

			public void HandleBasicConsumeOk(string consumerTag)
			{
			}

			public void HandleBasicDeliver(string consumerTag, ulong deliveryTag, bool redelivered, string exchange, string routingKey,
				IBasicProperties properties, byte[] body)
			{
				_action(body);
			}

			public void HandleModelShutdown(object model, ShutdownEventArgs reason)
			{
			}

			public IModel Model { get; private set; }
			public event EventHandler<ConsumerEventArgs> ConsumerCancelled;
		}
	}
}
