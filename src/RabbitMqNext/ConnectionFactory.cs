namespace RabbitMqNext
{
	using System;
	using System.Threading.Tasks;


	public class ConnectionFactory
	{
		public async Task<Connection> Connect(string hostname, 
			string vhost = "/", string username = "guest", 
			string password = "guest", int port = 5672)
		{
			var conn = new Connection();

			try
			{
				await conn.Connect(hostname, vhost, username, password, port);
			}
			catch (Exception)
			{
				conn.Dispose();
				throw;
			}
			
			return conn;
		}
	}
}