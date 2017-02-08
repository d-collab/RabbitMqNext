namespace PerfTestMultQClient
{
	using System;
	using System.Configuration;
	using System.Diagnostics;
	using System.Text;
	using System.Threading;
	using System.Threading.Tasks;
	using HdrHistogram;
	using RabbitMqNext;
	using RabbitMQ.Client;
	using RabbitMQ.Client.Events;


	class PerfTestMultQClientProgram
	{
		private ManualResetEventSlim _startSync;
		private LongHistogram _hdrHistogram;
		private CountdownEvent _completionSemaphore;

		static void Main(string[] args)
		{
			new PerfTestMultQClientProgram().Run(args).Wait();
		}

		private async Task Run(string[] args)
		{
			// 
			// client side 
			// sends rpc requests, measures the roundtrip
			// 

			LogAdapter.ExtendedLogEnabled = false;
			LogAdapter.ProtocolLevelLogEnabled = false;
			LogAdapter.LogDebugFn = (s, s1, arg3) => { };

			_hdrHistogram = new LongHistogram(1, 1000*10, 5);

			var host = ConfigurationManager.AppSettings["rabbit.host"];
			var user = ConfigurationManager.AppSettings["rabbit.admuser"];
			var pwd = ConfigurationManager.AppSettings["rabbit.admpwd"];
			var vhost = ConfigurationManager.AppSettings["rabbit.vhost"];

			var postWarmupDelay = TimeSpan.FromSeconds(5);

			int howManyQueues = 1;
			bool exclusiveConnections = ConfigurationManager.AppSettings["exclusiveConnections"] == "true";
			bool useOfficialClient = ConfigurationManager.AppSettings["useOfficialClient"] == "true";

			var howManyCalls = 50000;
//			var howManyCalls = 100;
			_completionSemaphore = new CountdownEvent(howManyQueues);
			_startSync = new ManualResetEventSlim(false);

//			if (useOfficialClient)
//			{
//				// Warm up
//				{
//					var connFac = new RabbitMQ.Client.ConnectionFactory()
//					{
//						HostName = host,
//						UserName = user,
//						Password = pwd,
//						VirtualHost = vhost,
//						AutomaticRecoveryEnabled = false
//					};
//					var conn2 = connFac.CreateConnection();
//					var channel2 = conn2.CreateModel();
//					var q = "q." + 0;
//					SendLegacyCalls(q, channel2, howManyCalls/4, isWarmUp: true);
//					channel2.Dispose();
//					conn2.Dispose();
//				}
//
//				await WarmupComplete(postWarmupDelay);
//
//				RabbitMQ.Client.IConnection conn = null;
//				RabbitMQ.Client.IModel channel = null;
//
//				for (int i = 0; i < howManyQueues; i++)
//				{
//					var connFac = new RabbitMQ.Client.ConnectionFactory()
//					{
//						HostName = host,
//						UserName = user,
//						Password = pwd,
//						VirtualHost = vhost,
//						AutomaticRecoveryEnabled = false
//					};
//
//					if (exclusiveConnections || conn == null)
//					{
//						conn = connFac.CreateConnection();
//						channel = conn.CreateModel();
//					}
//
//					var q = "q." + i;
//
//					new Thread(() =>
//					{
//						SendLegacyCalls(q, channel, howManyCalls, isWarmUp: false);
//					}) {IsBackground = true}.Start();
//
//				}
//			}
//			else
			{
				RabbitMqNext.LogAdapter.LogErrorFn = (scope, message, exc) =>
				{
					Console.WriteLine("[Error] " + scope + " - " + message + " exception " + exc);
				};
				RabbitMqNext.LogAdapter.LogWarnFn = (scope, message, exc) =>
				{
					Console.WriteLine("[Warn] " + scope + " - " + message + " exception " + exc);
				};
				RabbitMqNext.LogAdapter.LogDebugFn = (scope, message, exc) =>
				{
					Console.WriteLine("[Dbg] " + scope + " - " + message + " exception " + exc);
				};

				// Warm up
//				{
//					var conn2 =
//						await
//							RabbitMqNext.ConnectionFactory.Connect(host, vhost, user, pwd, recoverySettings: null,
//								connectionName: "perf_client");
//
//					var channel2 = await conn2.CreateChannel();
//					var q = "q." + 0;
//					await Task.Delay(1); // Thread switch
//					await SendModernCalls(q, channel2, howManyCalls/4, isWarmUp: true);
//					await Task.Delay(1); // Thread switch
//					channel2.Dispose();
//					conn2.Dispose();
//				}
//
//				await WarmupComplete(postWarmupDelay);

				Console.WriteLine("Will initiate...");

				RabbitMqNext.IConnection conn = null;
				RabbitMqNext.IChannel channel = null;

				for (int i = 0; i < howManyQueues; i++)
				{
					if (exclusiveConnections || conn == null)
					{
						conn =
							await
								RabbitMqNext.ConnectionFactory.Connect(host, vhost, user, pwd, recoverySettings: null,
									connectionName: "perf_client");
						channel = await conn.CreateChannel();
					}

					var q = "q." + i;

					new Thread(async () =>
					{
						await SendModernCalls(q, channel, howManyCalls, isWarmUp: false);

					}) {IsBackground = true}.Start();
				}
			}

			_startSync.Set();

			Console.WriteLine("Waiting completion");

			await Task.Delay(1); // switch

			_completionSemaphore.Wait();

			Console.WriteLine("Done\r\n");
			Console.WriteLine("howManyQueues {0} exclusive Connections: {1} official client: {2} count {3}", howManyQueues,
				exclusiveConnections, useOfficialClient, _hdrHistogram.TotalCount);
			Console.WriteLine("\r\n");

			_hdrHistogram.OutputPercentileDistribution(Console.Out);

			Console.ReadKey();
		}

		private async Task SendModernCalls(string queue, IChannel channel, int howManyCalls, bool isWarmUp)
		{
			try
			{
				if (!isWarmUp) _startSync.Wait();

				var helper = await channel.CreateRpcHelper(ConsumeMode.SingleThreaded, null);
				var buffer = Encoding.UTF8.GetBytes("Some content not too big or too short I think");
				var watch = new Stopwatch();

				for (int i = 0; i < howManyCalls; i++)
				{
//					Console.WriteLine("Calling " + i);
					watch.Restart();
					await helper.Call("", queue, channel.RentBasicProperties(), new ArraySegment<byte>(buffer), false);
					watch.Stop();

					if (!isWarmUp) RecordValue(watch);
				}

				if (!isWarmUp) _completionSemaphore.Signal();
			}
			catch (Exception ex)
			{
				Console.WriteLine(ex);
			}
		}

		private void SendLegacyCalls(string queue, IModel channel, int howManyCalls, bool isWarmUp)
		{
			if (!isWarmUp)
			{
				_startSync.Wait();
			}

			var waitForReply = new AutoResetEvent(false);
			var tempQueue = channel.QueueDeclare();

			channel.BasicConsume(tempQueue.QueueName, true, new DelegateConsumer((delivery, prop, body) =>
			{
				waitForReply.Set();
			}));

			var buffer = Encoding.UTF8.GetBytes("Some content not too big or too short I think");
			var watch = new Stopwatch();

			for (int i = 0; i < howManyCalls; i++)
			{
				watch.Restart();
				var prop = channel.CreateBasicProperties();
				prop.ReplyTo = tempQueue.QueueName;
				prop.CorrelationId = queue;

				channel.BasicPublish("", queue, prop, buffer);
				waitForReply.WaitOne();
				watch.Stop();

				if (!isWarmUp) RecordValue(watch);
			}

			if (!isWarmUp)
			{
				_completionSemaphore.Signal();
			}
		}

		private void RecordValue(Stopwatch watch)
		{
			lock (_hdrHistogram)
			_hdrHistogram.RecordValue(watch.ElapsedMilliseconds);
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
