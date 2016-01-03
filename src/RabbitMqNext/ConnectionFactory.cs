namespace RabbitMqNext
{
	using System;
	using System.Threading.Tasks;
	using Castle.Core.Logging;

	public class ConnectionFactory
	{
//		public ILogger Logger { get; set; }

		public ConnectionFactory()
		{
		}

		public async Task<Connection> Connect(string hostname,
			string vhost = "/", string username = "guest", string password = "guest", int port = 5672)
		{
			var conn = new Connection();

			try
			{
				await conn.Connect(hostname, vhost, username, password, port);
			}
			catch (Exception e)
			{
				conn.Close();
			}
			
			return conn;
		}
	}
}