namespace PerfTestServer
{
	using System;
	using System.Runtime;
	using System.Threading;
	using System.Threading.Tasks;
	using RabbitMqNext;

	class ServerProgram
	{
		const ushort Prefetch = 350;

		const bool WithAcks = false;

		// const int TotalPublish = 250000;
		const int TotalPublish = 100;
		// const int TotalPublish = 100000;
		// const int TotalPublish = 500000;

		const string TargetHost = "localhost";
		const string VHost = "clear_test";

		static void Main(string[] args)
		{
			Console.WriteLine("Is Server GC: " + GCSettings.IsServerGC);
			GCSettings.LargeObjectHeapCompactionMode = GCLargeObjectHeapCompactionMode.CompactOnce;
			Console.WriteLine("Compaction mode: " + GCSettings.LargeObjectHeapCompactionMode);
			Console.WriteLine("Latency mode: " + GCSettings.LatencyMode);
			GCSettings.LatencyMode = GCLatencyMode.SustainedLowLatency;
			Console.WriteLine("New Latency mode: " + GCSettings.LatencyMode);

			Console.WriteLine("I'm the server!");


			var t1 = StartRpcServer();
			// var t2 = StartRpcServer();

			Task.WaitAll(t1);

			Console.WriteLine("All done");

			Thread.CurrentThread.Join();
		}

		private static async Task<bool> StartRpcServer()
		{
			Connection conn1 = null;

			try
			{
				conn1 = await new ConnectionFactory().Connect(TargetHost, vhost: VHost);

				Console.WriteLine("[Connected]");

				var newChannel = await conn1.CreateChannel();
				Console.WriteLine("[channel created] " + newChannel.ChannelNumber);
				await newChannel.BasicQos(0, Prefetch, false);

				await newChannel.ExchangeDeclare("test_ex", "direct", true, false, null, true);

				var qInfo = await newChannel.QueueDeclare("rpc1", false, true, false, false, null, true);

				Console.WriteLine("[qInfo] " + qInfo);

				await newChannel.QueueBind("rpc1", "test_ex", "rpc1", null, true);


				Console.WriteLine("Starting Rpc channel Parallel consumer...");
				await newChannel.BasicConsume(ConsumeMode.SingleThreaded, delivery =>
				{
					var temp = new byte[100];

					if (delivery.stream != null)
						delivery.stream.Read(temp, 0, (int)delivery.bodySize);
//					else
//						temp = delivery.Body;

					var x = BitConverter.ToInt32(temp, 0);
//					Console.WriteLine("Got request " + x);

					var replyProp = new BasicProperties()
					{
						CorrelationId = delivery.properties.CorrelationId
					};

					// send reply
					newChannel.BasicPublishN("",
						delivery.properties.ReplyTo, true, false,
						replyProp, new ArraySegment<byte>(temp, 0, 4));

					return Task.CompletedTask;

				}, "rpc1", "", true, false, null, waitConfirmation: true);

				await Task.Delay(TimeSpan.FromMinutes(12));

				Console.WriteLine("Closing...");

				await newChannel.Close();
			}
			catch (AggregateException ex)
			{
				Console.WriteLine("[Captured error] " + ex.Flatten().Message);
			}
			catch (Exception ex)
			{
				Console.WriteLine("[Captured error 2] " + ex.Message);
			}

			if (conn1 != null)
				await conn1.Close();

			return true;
		}
	}
}
