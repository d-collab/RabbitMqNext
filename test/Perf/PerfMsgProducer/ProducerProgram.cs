namespace PerfMsgProducer
{
	using System;
	using System.Threading.Tasks;
	using System.Configuration;
	using System.Diagnostics;
	using System.IO;
	using System.Runtime;
	using PerfTest;
	using RabbitMqNext;

	class ProducerProgram
	{
		private static string _targetHost;
		private static string _username;
		private static string _password;
		private static string _vhost;

		private static int TotalPublish = 100000;

		static void Main(string[] args)
		{
			Console.WriteLine("Is Server GC: " + GCSettings.IsServerGC);
			GCSettings.LargeObjectHeapCompactionMode = GCLargeObjectHeapCompactionMode.CompactOnce;
			Console.WriteLine("Compaction mode: " + GCSettings.LargeObjectHeapCompactionMode);
			Console.WriteLine("Latency mode: " + GCSettings.LatencyMode);
			GCSettings.LatencyMode = GCLatencyMode.SustainedLowLatency;
			Console.WriteLine("New Latency mode: " + GCSettings.LatencyMode);

			_targetHost = ConfigurationManager.AppSettings["rabbitmqserver"];
			_username = ConfigurationManager.AppSettings["username"];
			_password = ConfigurationManager.AppSettings["password"];
			_vhost = ConfigurationManager.AppSettings["vhost"];

			var r = StartProducing().Result;

			Console.WriteLine("Done");
		}

		private static async Task<bool> StartProducing()
		{
			IConnection conn1 = null;
			conn1 = await new ConnectionFactory().Connect(_targetHost, vhost: _vhost, username: _username, password: _password);

			var newChannel = await conn1.CreateChannel();
			Console.WriteLine("[channel created] " + newChannel.ChannelNumber);

			await newChannel.ExchangeDeclare("test_ex", "direct", true, false, null, true);

			var qInfo = await newChannel.QueueDeclare("perf1", false, true, false, false, null, true);

			Console.WriteLine("[qInfo] " + qInfo);

			await newChannel.QueueBind("perf1", "test_ex", "perf1", null, true);

			Console.WriteLine("Starting producer...");

			for (int i = 0; i < TotalPublish; i++)
			{
				var msg = new Messages1 
					//{Seq = i, Ticks = Stopwatch.GetTimestamp()};
					{
						Seq = i, 
						// Ticks = 1,
						Something = "something _" + i + "_"
					};

				var stream = new MemoryStream();
				ProtoBuf.Meta.RuntimeTypeModel.Default.Serialize(stream, msg);
				var buffer = stream.GetBuffer();
				var len = (int) stream.Position;

				newChannel.BasicPublishFast("test_ex", "perf1", false, 
					new BasicProperties { Type = typeof(Messages1).FullName }, 
					new ArraySegment<byte>(buffer, 0, len));
			}

			await Task.Delay(TimeSpan.FromMinutes(1));

			await newChannel.Close();

			conn1.Dispose();

			return true;
		}
	}
}
