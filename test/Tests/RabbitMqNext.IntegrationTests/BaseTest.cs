namespace RabbitMqNext.IntegrationTests
{
	using System;
	using System.Configuration;
	using System.Linq;
	using System.Threading.Tasks;
	using MConsole;
	using NUnit.Framework;

	public class BaseTest
	{
		private IConnection _conn;

		protected string _host, _vhost, _username, _password;

		public BaseTest()
		{
			LogAdapter.ExtendedLogEnabled = true;
//			LogAdapter.ProtocolLevelLogEnabled = true;

			_host = ConfigurationManager.AppSettings["rabbitmqserver"];
			_vhost = ConfigurationManager.AppSettings["vhost"];
			_username = ConfigurationManager.AppSettings["username"];
			_password = ConfigurationManager.AppSettings["password"];
		}

		public async Task<Connection> StartConnection(bool autoRecovery = false)
		{
			using (var console = new RestConsole(_host, _username, _password))
			{
				var vhosts = await console.GetVHosts();
				var users = await console.GetUsers();
				Console.WriteLine("vhosts: " + vhosts.Aggregate(" ", (agg, vhst) => agg + " " + vhst.Name));
				Console.WriteLine("users: " + users.Aggregate(" ", (agg, u) => agg + " " + u.Name + "[" + u.Tags + "]"));

				if (!vhosts.Any(v => v.Name == _vhost))
				{
					await console.CreateVHost(_vhost);
					await console.SetUserVHostPermission(_username, _vhost);
				}
			}

			LogAdapter.LogDebugFn = (cat, msg, exc) =>
			{
				Console.WriteLine("DEBUG [{0}] : {1} - {2}", cat, msg, exc);
			};
			LogAdapter.LogErrorFn = (cat, msg, exc) =>
			{
				var color = Console.ForegroundColor;
				Console.ForegroundColor = ConsoleColor.Red;
				Console.WriteLine("ERROR [{0}] : {1} - {2}", cat, msg, exc);
				Console.ForegroundColor = color;
			};
			LogAdapter.LogWarnFn = (cat, msg, exc) =>
			{
				var color = Console.ForegroundColor;
				Console.ForegroundColor = ConsoleColor.Magenta;
				Console.WriteLine("WARN  [{0}] : {1} - {2}", cat, msg, exc);
				Console.ForegroundColor = color;
			};

			var conn = (Connection) await new ConnectionFactory().Connect(_host, _vhost, _username, _password, autoRecovery: autoRecovery);
			_conn = conn;
			return conn;
		}

		[TearDown]
		public void EndTest()
		{
			if (_conn != null)
			{
				_conn.Dispose();
				_conn = null;
			}
		}
	}
}