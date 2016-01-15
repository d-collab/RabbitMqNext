namespace RabbitMqNext.IntegrationTests
{
	using System.Configuration;
	using System.Threading.Tasks;
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