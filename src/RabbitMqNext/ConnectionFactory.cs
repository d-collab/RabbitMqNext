namespace RabbitMqNext
{
	using System;
	using System.Collections.Generic;
	using System.Threading.Tasks;
	using Recovery;


	public static class ConnectionFactory
	{
		public static async Task<IConnection> Connect(IEnumerable<string> hostnames,
			string vhost = "/", string username = "guest",
			string password = "guest", int port = 5672, 
			bool autoRecovery = true, string connectionName = null)
		{
			var conn = new Connection();

			try
			{
				foreach (var hostname in hostnames)
				{
					var successful = 
						await conn.Connect(hostname, vhost, 
										   username, password, port, connectionName, 
										   throwOnError: false).ConfigureAwait(false);
					if (successful)
					{
						LogAdapter.LogWarn("ConnectionFactory", "Selected " + hostname);

						return autoRecovery ? 
							(IConnection) new RecoveryEnabledConnection(hostnames, conn) : 
							conn;
					}
				}

				// TODO: collect exceptions and add them to aggregateexception:
				throw new AggregateException("Could not connect to any of the provided hosts");
			}
			catch (Exception e)
			{
				LogAdapter.LogError("ConnectionFactory", "Connection error", e);

				conn.Dispose();
				throw;
			}
		}

		public static async Task<IConnection> Connect(string hostname, 
			string vhost = "/", string username = "guest",
			string password = "guest", int port = 5672,
			bool autoRecovery = true, string connectionName = null)
		{
			var conn = new Connection();

			try
			{
				await conn
					.Connect(hostname, vhost, username, password, port, connectionName, throwOnError: true)
					.ConfigureAwait(false);

				if (LogAdapter.ExtendedLogEnabled)
					LogAdapter.LogDebug("ConnectionFactory", "Connected to " + hostname + ":" + port);

				return autoRecovery ? (IConnection)new RecoveryEnabledConnection(hostname, conn) : conn;
			}
			catch (Exception e)
			{
				LogAdapter.LogError("ConnectionFactory", "Connection error: " + hostname + ":" + port, e);

				conn.Dispose();
				throw;
			}
		}
	}
}