namespace PerfTestMultQServer
{
	using System;
	using System.Configuration;
	using System.Diagnostics;
	using System.IO;
	using System.Threading;
	using System.Threading.Tasks;
	using HdrHistogram;
	using RabbitMqNext;
	using RabbitMQ.Client;
	using RabbitMQ.Client.Events;

	class PerfTestMultQServerConsumerProgram
	{
		private static LongHistogram _hdrHistogram;

		static void Main(string[] args)
		{
			new PerfTestMultQServerConsumerProgram().Run(args).Wait();
		}

		private async Task Run(string[] args)
		{
			var host = ConfigurationManager.AppSettings["rabbit.host"];
			var user = ConfigurationManager.AppSettings["rabbit.admuser"];
			var pwd = ConfigurationManager.AppSettings["rabbit.admpwd"];
			var vhost = ConfigurationManager.AppSettings["rabbit.vhost"];

			LogAdapter.LogDebugFn = (s, s1, arg3) => { };
			LogAdapter.ExtendedLogEnabled = false;
			LogAdapter.ProtocolLevelLogEnabled = false;

			_hdrHistogram = new LongHistogram(1, 1000 * 10, 5);

			int howManyQueues = 10;
			bool exclusiveConnections = ConfigurationManager.AppSettings["exclusiveConnections"] == "true";
			bool useOfficialClient = ConfigurationManager.AppSettings["useOfficialClient"] == "true";

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
						channel.BasicQos(0, 100, false);
					}

					var q = "q." + i;
					channel.QueueDeclareNoWait(q, durable: true, exclusive: false, autoDelete: false, arguments: null);

					channel.BasicConsume(q, false, "con_" + q, arguments: null, consumer: new Consumer(channel));
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
						conn = await RabbitMqNext.ConnectionFactory.Connect(host, vhost, user, pwd, recoverySettings: null, connectionName: "mod_perf_server");
						channel = await conn.CreateChannel();
						await channel.BasicQos(0, 100, false);
					}

					var q = "q." + i;
					
					await channel.QueueDeclare(q, passive: false, durable: true, exclusive: false, autoDelete: false, arguments: null,
						waitConfirmation: false);

					// TODO: test with parallel buffer copy + serialized too
					await channel.BasicConsume(ConsumeMode.SerializedWithBufferCopy, BuildConsumerFn(channel), q, "consumer_" + q, 
											   false, true, arguments: null, waitConfirmation: false);
				}
			}

			Console.WriteLine("Consuming..");

			Console.CancelKeyPress += (sender, eventArgs) =>
			{
				Console.WriteLine("Done\r\n");
				Console.WriteLine("howManyQueues {0} exclusive Connections: {1} official client: {2} count {3}", howManyQueues, exclusiveConnections, useOfficialClient, _hdrHistogram.TotalCount);
				Console.WriteLine("\r\n");

				_hdrHistogram.OutputPercentileDistribution(Console.Out);

				Console.ReadKey();
			};

			await Task.Delay(1);

			Thread.CurrentThread.Join();
		}

		private static readonly long TickFreq = 1000 / Stopwatch.Frequency;

		private Func<MessageDelivery, Task> BuildConsumerFn(IChannel channel)
		{
			return async delivery =>
			{
				var message = (MessageWithTick) ProtoBuf.Meta.RuntimeTypeModel.Default.Deserialize(delivery.stream, null, typeof(MessageWithTick), delivery.bodySize);

				channel.BasicAck(delivery.deliveryTag, false);

				if (message.IsWarmUp) return;

				var diff = Stopwatch.GetTimestamp() - message.SentAtInTicks;

//				lock (_hdrHistogram)
//					_hdrHistogram.RecordValue(diff * TickFreq);
			};
		}

		internal class Consumer : IBasicConsumer
		{
			private readonly IModel _channel;

			public Consumer(IModel channel)
			{
				_channel = channel;
			}

			public void HandleBasicDeliver(string consumerTag, ulong deliveryTag, bool redelivered, string exchange, string routingKey,
				IBasicProperties properties, byte[] body)
			{
				var message = (MessageWithTick) ProtoBuf.Meta.RuntimeTypeModel.Default.Deserialize(new MemoryStream(body), null, typeof(MessageWithTick));

				_channel.BasicAck(deliveryTag, false);

				if (message.IsWarmUp) return;

				var diff = Stopwatch.GetTimestamp() - message.SentAtInTicks;

//				lock (_hdrHistogram)
//					_hdrHistogram.RecordValue(diff * TickFreq);
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

			public IModel Model { get { return _channel; } }
			public event EventHandler<ConsumerEventArgs> ConsumerCancelled;
		}
	}
}
