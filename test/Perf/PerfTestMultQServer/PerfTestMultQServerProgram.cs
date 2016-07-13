namespace PerfTestMultQServer
{
	using System;
	using System.Collections.Generic;
	using System.Configuration;
	using System.Threading;
	using System.Threading.Tasks;
	using RabbitMqNext;
	using RabbitMQ.Client;
	using RabbitMQ.Client.Events;

	class PerfTestMultQServerProgram
	{
		static void Main(string[] args)
		{
			new PerfTestMultQServerProgram().Run(args).Wait();
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


			int howManyQueues = 3;
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
					}

					var q = "q." + i;
					
					await channel.QueueDeclare(q, passive: false, durable: true, exclusive: false, autoDelete: false, arguments: null,
						waitConfirmation: false);

					await channel.BasicConsume(ConsumeMode.ParallelWithBufferCopy, BuildConsumerFn(channel), q, "consumer_" + q, 
											   false, true, arguments: null, waitConfirmation: false);
				}
			}

			Console.WriteLine("Ready");

			Thread.CurrentThread.Join();
		}

		private Func<MessageDelivery, Task> BuildConsumerFn(IChannel channel)
		{
			return async delivery =>
			{
				var data = new byte[1024];
				var read = 0;
				while (read < delivery.bodySize)
				{
					read += delivery.stream.Read(data, read, delivery.bodySize - read);
				}

				var prop = channel.RentBasicProperties();
				prop.CorrelationId = delivery.properties.CorrelationId;

				channel.BasicPublishFast("", delivery.properties.ReplyTo, false, prop, new ArraySegment<byte>(data, 0, read));

				channel.BasicAck(delivery.deliveryTag, false);
			};
		}
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
			var data = new byte[body.Length];
			// to be equal comparisson, need to copy buffer
			Buffer.BlockCopy(body, 0, data, 0, body.Length);

			var repProp = _channel.CreateBasicProperties();

			repProp.CorrelationId = properties.CorrelationId;

			_channel.BasicPublish("", properties.ReplyTo, false, repProp, data);

			_channel.BasicAck(deliveryTag, false);
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
