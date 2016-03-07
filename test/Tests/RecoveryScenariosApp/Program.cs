namespace RecoveryScenariosApp
{
	using System;
	using System.Configuration;
	using System.Threading;
	using System.Threading.Tasks;
	using RabbitMqNext;

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
			LogAdapter.ProtocolLevelLogEnabled = true;

			_host = ConfigurationManager.AppSettings["rabbitmqserver"];
			_vhost = ConfigurationManager.AppSettings["vhost"];
			_username = ConfigurationManager.AppSettings["username"];
			_password = ConfigurationManager.AppSettings["password"];

			var t = new Task<bool>(() => Start().Result, TaskCreationOptions.LongRunning);
			t.Start();
			t.Wait();
		}

		private static async Task<bool> Start()
		{
			var conn = (RecoveryEnabledConnection)
				await ConnectionFactory
					.Connect(_host, _vhost, 
					_username, _password, autoRecovery: true);

			Console.WriteLine("Do stop rabbitmq service");

			await Task.Delay(TimeSpan.FromMinutes(10));

			Console.WriteLine("Done. Disposing...");

			conn.Dispose();

			return true;
		}
	}
}
