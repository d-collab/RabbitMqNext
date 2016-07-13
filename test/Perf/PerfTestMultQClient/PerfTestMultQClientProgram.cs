namespace PerfTestMultQClient
{
	using System;
	using System.Collections.Generic;
	using System.Configuration;
	using System.Diagnostics;
	using System.Text;
	using System.Threading;
	using System.Threading.Tasks;
	using RabbitMqNext;
	using RabbitMQ.Client;

	class PerfTestMultQClientProgram
	{
		private ManualResetEventSlim _startSync;

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

					if (exclusiveConnections)
					{
						conn = connFac.CreateConnection();
						channel = conn.CreateModel();
					}
					else
					{
						if (conn == null)
						{
							conn = connFac.CreateConnection();
							channel = conn.CreateModel();
						}
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
					if (exclusiveConnections)
					{
						conn = await RabbitMqNext.ConnectionFactory.Connect(host, vhost, user, pwd, recoverySettings: null);
						channel = await conn.CreateChannel();
					}
					else
					{
						if (conn == null)
						{
							conn = await RabbitMqNext.ConnectionFactory.Connect(host, vhost, user, pwd, recoverySettings: null);
							channel = await conn.CreateChannel();
						}
					}

					var q = "q." + i;

					tasks.Add(Task.Run(() => SendModernCalls(q, channel, howManyCalls)));
				}
			}


			Console.WriteLine("Ready");

			Thread.CurrentThread.Join();
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
			}
		}

		private void SendLegacyCalls(string queue, IModel channel, int howManyCalls)
		{
			_startSync.Wait();

			var tempQueue = channel.QueueDeclare();

			for (int i = 0; i < howManyCalls; i++)
			{
				
			}
		}

	}
}
