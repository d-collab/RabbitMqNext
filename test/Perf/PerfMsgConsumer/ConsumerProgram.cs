namespace PerfMsgConsumer
{
	using System;
	using System.Text;
	using System.Threading.Tasks;
	using System.Configuration;
	using System.Diagnostics;
	using System.IO;
	using System.Runtime;
	using System.Threading;
	using PerfTest;
	using RabbitMqNext;
	using RabbitMqNext.Internals.RingBuffer;

	class ConsumerProgram
	{
		private static string _targetHost;
		private static string _username;
		private static string _password;
		private static string _vhost;

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

			var r = StartConsumer().Result;

			Console.WriteLine("Done");
		}

		private static async Task<bool> StartConsumer()
		{
			Connection conn1 = null;
			conn1 = await new ConnectionFactory().Connect(_targetHost, vhost: _vhost, username: _username, password: _password);

			var newChannel = await conn1.CreateChannel();
			Console.WriteLine("[channel created] " + newChannel.ChannelNumber);
			await newChannel.BasicQos(0, 5, false);

			await newChannel.ExchangeDeclare("test_ex", "direct", true, false, null, true);

			var qInfo = await newChannel.QueueDeclare("perf1", false, true, false, false, null, true);

			Console.WriteLine("[qInfo] " + qInfo);

			await newChannel.QueueBind("perf1", "test_ex", "perf1", null, true);

			Console.WriteLine("Starting consumer...");

			int count = 0;

			await newChannel.BasicConsume(ConsumeMode.ParallelWithReadBarrier, (delivery) =>
			{
				var buffer = new byte[delivery.bodySize];
				try
				{
					delivery.stream.Read(buffer, 0, buffer.Length);

					var msg = (Messages1)
						ProtoBuf.Meta.RuntimeTypeModel.Default.Deserialize(new MemoryStream(buffer), 
							null, typeof (Messages1), buffer.Length);

					Console.WriteLine(//"OK Received " + ToStr(buffer) + 
						" Count " + Interlocked.Increment(ref count) + " seq " + msg.Seq);
				}
				catch (Exception e)
				{
					Console.WriteLine("   Received " + ToStr(buffer) + " __ " + delivery.stream);
					Console.Error.WriteLine(e);
				}

				return Task.CompletedTask;

			}, "perf1", "", true, false, null, true);

			await Task.Delay(TimeSpan.FromMinutes(10));

			await newChannel.Close();

			conn1.Dispose();

			return true;
		}

		internal static string ToStr(byte[] buffer)
		{
			var sb = new StringBuilder(buffer.Length * 3);
			for (int i = 0; i < buffer.Length; i++)
			{
				sb.Append(buffer[i]).Append(' ');
			}
			return sb.ToString();
		}
	}
}
