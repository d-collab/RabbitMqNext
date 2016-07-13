namespace PerfTestMultQClient
{
	using System;
	using System.Collections.Generic;
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

			_hdrHistogram = new LongHistogram(10, 100000, 5);

			var host = ConfigurationManager.AppSettings["rabbit.host"];
			var user = ConfigurationManager.AppSettings["rabbit.admuser"];
			var pwd = ConfigurationManager.AppSettings["rabbit.admpwd"];
			var vhost = ConfigurationManager.AppSettings["rabbit.vhost"];


			int howManyQueues = 2;
			bool exclusiveConnections = ConfigurationManager.AppSettings["exclusiveConnections"] == "true";
			bool useOfficialClient = ConfigurationManager.AppSettings["useOfficialClient"] == "true";

			var howManyCalls = 100;
			var tasks = new List<Task>();
			_startSync = new ManualResetEventSlim(false);

			if (useOfficialClient)
			{
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

					tasks.Add(Task.Run(() => SendLegacyCalls(q, channel, howManyCalls)));
				}
			}
			else
			{
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

					tasks.Add(Task.Run(() => SendModernCalls(q, channel, howManyCalls)));
				}
			}

			Console.WriteLine("Waiting completion");

			Task.WaitAll(tasks.ToArray());

			Console.WriteLine("Done\r\n");
			Console.WriteLine("howManyQueues {0} exclusive Connections: {1} official client: {2}", howManyQueues, exclusiveConnections, useOfficialClient);
			Console.WriteLine("\r\n");

			_hdrHistogram.OutputPercentileDistribution(Console.Out);

			Console.ReadKey();
		}

		private async void SendModernCalls(string queue, IChannel channel, int howManyCalls)
		{
			_startSync.Wait();

			var helper = await channel.CreateRpcHelper(ConsumeMode.SingleThreaded, null);
			var buffer = Encoding.UTF8.GetBytes("Some content not too big or too short I think");
			var watch = new Stopwatch();

			for (int i = 0; i < howManyCalls; i++)
			{
				watch.Restart();
				await helper.Call("", queue, BasicProperties.Empty, new ArraySegment<byte>(buffer), false);
				watch.Stop();

				RecordValue(watch);
			}
		}

		private void SendLegacyCalls(string queue, IModel channel, int howManyCalls)
		{
			_startSync.Wait();

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
				channel.BasicPublish("", queue, prop, buffer);
				waitForReply.WaitOne();
				watch.Stop();

				RecordValue(watch);
			}
		}

		private void RecordValue(Stopwatch watch)
		{
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
