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
		private Connection _conn;

		public async Task<Connection> StartConnection()
		{
			var host = ConfigurationManager.AppSettings["rabbitmqserver"];
			var vhost = ConfigurationManager.AppSettings["vhost"];
			var username = ConfigurationManager.AppSettings["username"];
			var password = ConfigurationManager.AppSettings["password"];

			using (var console = new RestConsole(host, username, password))
			{
				var vhosts = await console.GetVHosts();
				var users = await console.GetUsers();
				Console.WriteLine("vhosts: " + vhosts.Aggregate(" ", (agg, vhst) => agg + " " + vhst.Name));
				Console.WriteLine("users: " + users.Aggregate(" ", (agg, u) => agg + " " + u.Name + "[" + u.Tags + "]"));

				if (!vhosts.Any(v => v.Name == vhost))
				{
					await console.CreateVHost(vhost);
					await console.SetUserVHostPermission(username, vhost);
				}
			}

			var conn = await new ConnectionFactory().Connect(host, vhost, username, password);
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