namespace RecoveryScenariosApp
{
	using System;
	using System.Configuration;
	using System.Text;
	using System.Threading;
	using System.Threading.Tasks;
	using RabbitMqNext;
	using RabbitMqNext.Recovery;

	class Program
	{
		private static string _host, _vhost, _username, _password;

		static void Main(string[] args)
		{
			// Scenarios to test:
			// - idle connection 
			// - pending writing commands
			// - after declares
			// - after bindings
			// - after acks/nacks
			// - consumers
			// - rpc

			// 
			// correct behavior
			// 
			// * upon abrupt disconnection
			// 1 - writeloop and readloop threads area cancelled
			// 2 - reset pending commands
			//     reset ring buffer (what about pending reads?)
			// 3 - recovery kicks in
			// 
			// * upon reconnection
			// 1 - socketholder is kept (so does ring buffer)
			// 2 - recover entities

			LogAdapter.ExtendedLogEnabled = true;
//			LogAdapter.ProtocolLevelLogEnabled = true;

			_host = ConfigurationManager.AppSettings["rabbitmqserver"];
			_vhost = ConfigurationManager.AppSettings["vhost"];
			_username = ConfigurationManager.AppSettings["username"];
			_password = ConfigurationManager.AppSettings["password"];

			var t = new Task<bool>(() => Start().Result, TaskCreationOptions.LongRunning);
			t.Start();
			t.Wait();
		}

		private static Timer _timer;

		private static byte[] Buffer1 = Encoding.ASCII.GetBytes("Some message");
		private static byte[] Buffer2 = Encoding.ASCII.GetBytes("Some kind of other message");

		private static async Task<bool> Start()
		{
			var replier = await StartRpcReplyThread();


			var conn = (RecoveryEnabledConnection)
				await ConnectionFactory
					.Connect(_host, _vhost, 
					_username, _password, autoRecovery: true);

			conn.RecoveryCompleted += async () =>
			{
				try
				{
					var channel = await conn.CreateChannel();
					await channel.BasicQos(0, 150, false);
				}
				catch (Exception ex)
				{
					Console.WriteLine("Error in ev " + ex);
					throw;
				}
			};

			var channel1 = await conn.CreateChannel();
			await channel1.ExchangeDeclare("exchange_rec_1", "direct", false, autoDelete: true, arguments: null, waitConfirmation: true);
			await channel1.QueueDeclare("qrpc1", false, true, false, true, null, waitConfirmation: true);
			await channel1.QueueDeclare("queue_rec_1", false, true, false, true, null, waitConfirmation: true);
			await channel1.QueueBind("queue_rec_1", "exchange_rec_1", "routing1", null, waitConfirmation: true);
			var rpcHelper = await channel1.CreateRpcHelper(ConsumeMode.ParallelWithBufferCopy, 1000);

			var channel2 = await conn.CreateChannel();
			await channel2.ExchangeDeclare("exchange_rec_2", "direct", false, autoDelete: true, arguments: null, waitConfirmation: true);
			await channel2.QueueDeclare("queue_rec_2", false, true, false, true, null, waitConfirmation: true);
			await channel2.QueueBind("queue_rec_2", "exchange_rec_2", "routing2", null, waitConfirmation: true);

			int counter = 0;

			_timer = new Timer(async state =>
			{
				Console.WriteLine("Sending message ..");

				try
				{
					await rpcHelper.Call("", "qrpc1", null, BitConverter.GetBytes(counter++));
					await channel1.BasicPublish("exchange_rec_1", "routing1", false, BasicProperties.Empty, Buffer1);
					await channel2.BasicPublish("exchange_rec_2", "routing2", false, BasicProperties.Empty, Buffer2);
				}
				catch (Exception ex)
				{
					Console.WriteLine("Error sending message " + ex.Message);
				}

			}, null, TimeSpan.FromSeconds(3), TimeSpan.FromSeconds(1));


			var channel3 = await conn.CreateChannel();
			await channel3.BasicConsume(ConsumeMode.ParallelWithBufferCopy, new Consumer("1"), "queue_rec_1", "", true, false, null, true);
			await channel3.BasicConsume(ConsumeMode.ParallelWithBufferCopy, new Consumer("2"), "queue_rec_2", "", true, false, null, true);



			Console.WriteLine("Do stop rabbitmq service");



			await Task.Delay(TimeSpan.FromMinutes(10));



			_timer.Dispose();

			Console.WriteLine("Done. Disposing...");

			conn.Dispose();

			replier.Dispose();

			return true;
		}

		private static async Task<IDisposable> StartRpcReplyThread()
		{
			var conn = (RecoveryEnabledConnection)
				await ConnectionFactory
					.Connect(_host, _vhost,
					_username, _password, autoRecovery: true);

			var channel = await conn.CreateChannel();

			await channel.QueueDeclare("qrpc1", false, true, false, true, null, waitConfirmation: true);

			await channel.BasicConsume(ConsumeMode.SingleThreaded, (delivery) =>
			{
				var prop = channel.RentBasicProperties();

				prop.CorrelationId = delivery.properties.CorrelationId;
				var buffer = new byte[4];
				delivery.stream.Read(buffer, 0, 4);

				Console.WriteLine("Got rpc request " + BitConverter.ToInt32(buffer, 0) + ". Sending rpc reply");

				channel.BasicPublishFast("", delivery.properties.ReplyTo, false, prop, Encoding.UTF8.GetBytes("Reply"));

				return Task.CompletedTask;

			}, "qrpc1", "", true, false, null, true);

			return (IDisposable) conn;
		}
	}
}
