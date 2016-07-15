namespace PerfTestMultQClient
{
	using System;
	using System.Configuration;
	using System.Diagnostics;
	using System.IO;
	using System.Text;
	using System.Threading;
	using System.Threading.Tasks;
	using HdrHistogram;
	using PerfTestMultQServer;
	using RabbitMqNext;
	using RabbitMQ.Client;
	using RabbitMQ.Client.Events;


	class PerfTestMultQClientPubProgram
	{
		private ManualResetEventSlim _startSync;
		private SemaphoreSlim _completionSemaphore;

		static void Main(string[] args)
		{
			new PerfTestMultQClientPubProgram().Run(args).Wait();
		}

		private async Task Run(string[] args)
		{
			// 
			// client side 
			// publishes on different connections
			// 

			LogAdapter.ExtendedLogEnabled = false;
			LogAdapter.ProtocolLevelLogEnabled = false;
			LogAdapter.LogDebugFn = (s, s1, arg3) => { };

			var host = ConfigurationManager.AppSettings["rabbit.host"];
			var user = ConfigurationManager.AppSettings["rabbit.admuser"];
			var pwd = ConfigurationManager.AppSettings["rabbit.admpwd"];
			var vhost = ConfigurationManager.AppSettings["rabbit.vhost"];

			var postWarmupDelay = TimeSpan.FromSeconds(5);

			int howManyQueues = 10;
			bool exclusiveConnections = ConfigurationManager.AppSettings["exclusiveConnections"] == "true";
			bool useOfficialClient = ConfigurationManager.AppSettings["useOfficialClient"] == "true";

			var howManyCalls = 100000;
			_completionSemaphore = new SemaphoreSlim(0, howManyQueues);
			_startSync = new ManualResetEventSlim(false);

			if (useOfficialClient)
			{
				// Warm up
//				{
//					var connFac = new RabbitMQ.Client.ConnectionFactory() { HostName = host, UserName = user, Password = pwd, VirtualHost = vhost, AutomaticRecoveryEnabled = false };
//					var conn2 = connFac.CreateConnection();
//					var channel2 = conn2.CreateModel();
//					var q = "q." + 0;
//					PublishLegacy(q, channel2, howManyCalls / 4, isWarmUp: true);
//					channel2.Dispose();
//					conn2.Dispose();
//				}
//
//				await WarmupComplete(postWarmupDelay);

				RabbitMQ.Client.IConnection conn = null;
				RabbitMQ.Client.IModel channel = null;

				for (int i = 0; i < howManyQueues; i++)
				{
					var connFac = new RabbitMQ.Client.ConnectionFactory() { HostName = host, UserName = user, Password = pwd, VirtualHost = vhost, AutomaticRecoveryEnabled = false };

					if (exclusiveConnections || conn == null)
					{
						conn = connFac.CreateConnection();
						channel = conn.CreateModel();
					}

					var q = "q." + i;

					new Thread(() =>
					{
						PublishLegacy(q, channel, howManyCalls, isWarmUp: false);
					}) { IsBackground = true }.Start();
				}
			}
			else
			{
				// Warm up
//				{
//					var conn2 = await RabbitMqNext.ConnectionFactory.Connect(host, vhost, user, pwd, recoverySettings: null, connectionName: "perf_client");
//					var channel2 = await conn2.CreateChannel();
//					var q = "q." + 0;
//					await Task.Delay(1); // Thread switch
//					PublishModern(q, channel2, howManyCalls / 4, isWarmUp: true);
//					await Task.Delay(1); // Thread switch
//					channel2.Dispose();
//					conn2.Dispose();
//				}
//
//				await WarmupComplete(postWarmupDelay);

				RabbitMqNext.IConnection conn = null;
				RabbitMqNext.IChannel channel = null;

				for (int i = 0; i < howManyQueues; i++)
				{
					if (exclusiveConnections || conn == null)
					{
						conn = await RabbitMqNext.ConnectionFactory.Connect(host, vhost, user, pwd, recoverySettings: null, connectionName: "perf_client");
						channel = await conn.CreateChannel();
					}

					var q = "q." + i;

					new Thread(() =>
					{
						PublishModern(q, channel, howManyCalls, isWarmUp: false);

					}) {IsBackground = true}.Start();
				}
			}

			_startSync.Set();

			Console.WriteLine("Waiting for publishing to complete");

			for (int i = 0; i < howManyQueues; i++) _completionSemaphore.Wait();

			Console.WriteLine("Done\r\n");

			Console.ReadKey();
		}

		private async void PublishModern(string queue, IChannel channel, int howManyCalls, bool isWarmUp)
		{
			if (!isWarmUp) _startSync.Wait();

			var message = new MessageWithTick()
			{
				IsWarmUp = isWarmUp,
				SomeRandomContent = "Some content not too big or too short I think",
			};

			var stream = new MemoryStream();

			for (int i = 0; i < howManyCalls; i++)
			{
				message.SentAtInTicks = Stopwatch.GetTimestamp();
				stream.Position = 0;
				ProtoBuf.Meta.RuntimeTypeModel.Default.Serialize(stream, message);

				var buffer = new byte[stream.Position];
				stream.Position = 0;
				stream.Read(buffer, 0, (int)buffer.Length);

				channel.BasicPublishFast("", queue, false, BasicProperties.Empty, buffer);

			}

			if (!isWarmUp) _completionSemaphore.Release();
		}

		private void PublishLegacy(string queue, IModel channel, int howManyCalls, bool isWarmUp)
		{
			if (!isWarmUp) _startSync.Wait();

			var message = new MessageWithTick()
			{
				IsWarmUp = isWarmUp,
				SomeRandomContent = "Some content not too big or too short I think",
			};

			var stream = new MemoryStream();
			var prop = channel.CreateBasicProperties();

			for (int i = 0; i < howManyCalls; i++)
			{
				message.SentAtInTicks = Stopwatch.GetTimestamp();
				stream.Position = 0;
				ProtoBuf.Meta.RuntimeTypeModel.Default.Serialize(stream, message);

				var buffer = new byte[stream.Position];
				stream.Position = 0;
				stream.Read(buffer, 0, (int)buffer.Length);

				channel.BasicPublish("", queue, prop, buffer);
			}

			if (!isWarmUp) _completionSemaphore.Release();
		}

		private async Task WarmupComplete(TimeSpan postWarmupDelay)
		{
			Console.WriteLine("Warm up done. Will initiate tests in " + postWarmupDelay.TotalSeconds + " seconds");

			await Task.Delay(postWarmupDelay);

			Console.WriteLine("Starting");
		}
	}

	internal class DelegateConsumer : IBasicConsumer
	{
		private readonly Action<ulong, IBasicProperties, byte[]> _action;

		public DelegateConsumer(Action<ulong, IBasicProperties, byte[]> action)
		{
			_action = action;
		}

		public void HandleBasicDeliver(string consumerTag, ulong deliveryTag, bool 
			redelivered, string exchange, string routingKey,
			IBasicProperties properties, byte[] body)
		{
			_action(deliveryTag, properties, body);
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


		public void HandleModelShutdown(object model, ShutdownEventArgs reason)
		{
		}

		public IModel Model { get; private set; }
		public event EventHandler<ConsumerEventArgs> ConsumerCancelled;
	}
}
