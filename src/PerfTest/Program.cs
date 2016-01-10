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
		const ushort Prefetch = 350;

		const bool WithAcks = false;

		const int TotalPublish = 250000;
//		const int TotalPublish = 10;
//		const int TotalPublish = 100000;
//		const int TotalPublish = 500000;

		const string TargetHost = "localhost";
		const string VHost = "clear_test";

		private static string Message =
			"The Completed event provides a way for client applications to " +
			"complete an asynchronous socket operation. An event handler should " +
			"be attached to the event within a SocketAsyncEventArgs instance when " +
			"an asynchronous socket operation is initiated, otherwise the application " +
			"will not be able to determine when the operation.";

		private static byte[] MessageContent = Encoding.UTF8.GetBytes(Message);

	    public static void Main()
	    {
			Console.WriteLine("Is Server GC: " + GCSettings.IsServerGC);
			GCSettings.LargeObjectHeapCompactionMode = GCLargeObjectHeapCompactionMode.CompactOnce;
			Console.WriteLine("Compaction mode: " + GCSettings.LargeObjectHeapCompactionMode);
			Console.WriteLine("Latency mode: " + GCSettings.LatencyMode);
			GCSettings.LatencyMode = GCLatencyMode.SustainedLowLatency;
			Console.WriteLine("New Latency mode: " + GCSettings.LatencyMode);

//		    SubscribeForGCNotifications();

			var t = Start(); // StartOriginalClient(); // Start();
		    t.Wait();

			Console.WriteLine("All done");

		    Thread.CurrentThread.Join();
	    }

		private static async Task Start()
		{
			Connection conn = null;
			try
			{
				conn = await new ConnectionFactory().Connect(TargetHost, vhost: VHost);

				Console.WriteLine("[Connected]");

				var newChannel = await conn.CreateChannel();
				Console.WriteLine("[channel created] " + newChannel.ChannelNumber);
				await newChannel.BasicQos(0, Prefetch, false);

				await newChannel.ExchangeDeclare("test_ex", "direct", true, false, null, true);

				var qInfo = await newChannel.QueueDeclare("queue1", false, true, false, false, null, true);

				Console.WriteLine("[qInfo] " + qInfo);

				await newChannel.QueueBind("queue1", "test_ex", "routing1", null, true);

				var prop = new BasicProperties()
				{
					// DeliveryMode = 2,
					Type = "type1",
					Headers = new Dictionary<string, object> {{"serialization", 0}}
				};

				newChannel.MessageUndeliveredHandler = (undelivered) =>
				{
					Console.WriteLine("\t(Ops, message not routed: " + 
						undelivered.replyCode + " " + 
						undelivered.replyText + " " + 
						undelivered.routingKey + ")");

					return Task.CompletedTask;
				};

				Console.WriteLine("Started Publishing...");

				var watch = Stopwatch.StartNew();
				for (int i = 0; i < TotalPublish; i++)
				{
					prop.Headers["serialization"] = i;
					// var buffer = Encoding.ASCII.GetBytes("The " + i + " " + Message);
					await newChannel.BasicPublish("test_ex", "routing1", false, false, prop, new ArraySegment<byte>(MessageContent));

					// await Task.Delay(TimeSpan.FromMilliseconds(100));
				}
				watch.Stop();

				Console.WriteLine("BasicPublish stress. Took " + watch.Elapsed.TotalMilliseconds + 
								  "ms - rate of " + (TotalPublish / watch.Elapsed.TotalSeconds) + " message per second");

				var newChannel2 = await conn.CreateChannel();
				Console.WriteLine("[channel created] " + newChannel2.ChannelNumber);
				await newChannel2.BasicQos(0, Prefetch, false);
 
				var temp = new byte[1000000];

				watch = Stopwatch.StartNew();
				int totalReceived = 0;

				Console.WriteLine("[subscribing to queue] ");
				var sub = await newChannel2.BasicConsume(async (delivery) =>
				{
					// var len = await delivery.stream.ReadAsync(temp, 0, (int) delivery.bodySize);
					// var str = Encoding.UTF8.GetString(temp, 0, len);
					// Console.WriteLine("Received : " + str.Length);

					if (WithAcks)
					{
						if (totalReceived % 2 == 0)
							await newChannel2.BasicAck(delivery.deliveryTag, false);
						else
							await newChannel2.BasicNAck(delivery.deliveryTag, false, false);
					}

					// newChannel2.BasicAck()

					if (++totalReceived == TotalPublish)
					{
						watch.Stop();
						Console.WriteLine("Consume stress. Took " + 
										  watch.Elapsed.TotalMilliseconds + 
										  "ms - rate of " + (TotalPublish / watch.Elapsed.TotalSeconds) + " message per second");
						totalReceived = 0;
					}

					// return Task.CompletedTask;

				}, "queue1", "tag123", !WithAcks, false, null, true);

				Console.WriteLine("[subscribed to queue] " + sub);

				await Task.Delay(TimeSpan.FromMinutes(1));

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

			if (conn != null)
				await conn.Close();
		}

		private static async Task StartOriginalClient()
		{
			var conn = new RabbitMQ.Client.ConnectionFactory()
			{
				HostName = TargetHost,
				VirtualHost = VHost,
				UserName = "guest",
				Password = "guest"
			}.CreateConnection();

			var channel = conn.CreateModel();
			channel.BasicQos(0, Prefetch, false);

			channel.ExchangeDeclare("test_ex", "direct", true, false, null);

			channel.QueueDeclare("queue1", true, false, false, null);

			channel.QueueBind("queue1", "test_ex", "routing2", null);

			var prop = channel.CreateBasicProperties();
			prop.Type = "type1";
			// DeliveryMode = 2,
			prop.Headers = new Dictionary<string, object> { { "serialization", 0 } };

			Console.WriteLine("Started Publishing...");

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
			channel.BasicConsume("queue1", !WithAcks, new OldStyleConsumer((deliveryTag, prop2, body) =>
			{
				if (WithAcks)
				{
					if (totalReceived%2 == 0)
						channel.BasicAck(deliveryTag, false);
					else
						channel.BasicNack(deliveryTag, false, false);
				}

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

		private static void SubscribeForGCNotifications()
		{
			GC.RegisterForFullGCNotification(1, 1);

			Task.Factory.StartNew(() =>
			{
				while (true)
				{
					GCNotificationStatus s = GC.WaitForFullGCApproach();
					if (s == GCNotificationStatus.Succeeded)
					{
						Console.WriteLine("GC Notification raised.");
						// OnFullGCApproachNotify();
					}
					else if (s == GCNotificationStatus.Canceled)
					{
						Console.WriteLine("GC Notification cancelled.");
						break;
					}
					else
					{
						// This can occur if a timeout period
						// is specified for WaitForFullGCApproach(Timeout) 
						// or WaitForFullGCComplete(Timeout)  
						// and the time out period has elapsed. 
						Console.WriteLine("GC Notification not applicable.");
						break;
					}

					// Check for a notification of a completed collection.
					s = GC.WaitForFullGCComplete();
					if (s == GCNotificationStatus.Succeeded)
					{
						Console.WriteLine("GC Notifiction raised.");
						// OnFullGCCompleteEndNotify();
					}
					else if (s == GCNotificationStatus.Canceled)
					{
						Console.WriteLine("GC Notification cancelled.");
						break;
					}
					else
					{
						// Could be a time out.
						Console.WriteLine("GC Notification not applicable.");
						break;
					}
				}
			}, TaskCreationOptions.LongRunning);
		}

		class OldStyleConsumer : IBasicConsumer
		{
			private readonly Action<ulong, IBasicProperties, byte[]> _action;

			public OldStyleConsumer(Action<ulong, IBasicProperties, byte[]> action)
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
				_action(deliveryTag, properties, body);
			}

			public void HandleModelShutdown(object model, ShutdownEventArgs reason)
			{
			}

			public IModel Model { get; private set; }
			public event EventHandler<ConsumerEventArgs> ConsumerCancelled;
		}
	}
}
