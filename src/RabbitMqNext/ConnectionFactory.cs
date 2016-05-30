namespace RabbitMqNext
{
	using System;
	using System.Collections.Generic;
	using System.Threading.Tasks;
	using Recovery;


	public class AutoRecoverySettings
	{
		public static readonly AutoRecoverySettings Off = new AutoRecoverySettings { Enabled = false };
		public static readonly AutoRecoverySettings All = new AutoRecoverySettings { Enabled = true };
		public static readonly AutoRecoverySettings AllExceptBindings = new AutoRecoverySettings { Enabled = true, RecoverBindings = false };

		public bool Enabled { get; set; }
		
		/// <summary>
		/// 
		/// </summary>
		public bool RecoverBindings { get; set; }
	}

	public static class ConnectionFactory
	{
		private const string DefaultConnectionName = "unnamed_connection";

		public static async Task<IConnection> Connect(IEnumerable<string> hostnames,
			string vhost = "/", string username = "guest",
			string password = "guest", int port = 5672, 
			AutoRecoverySettings recoverySettings = null, string connectionName = null)
		{
			recoverySettings = recoverySettings ?? AutoRecoverySettings.Off;
			connectionName = connectionName ?? DefaultConnectionName;

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

						return recoverySettings.Enabled ?
							(IConnection) new RecoveryEnabledConnection(hostnames, conn, recoverySettings) : 
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
			AutoRecoverySettings recoverySettings = null, string connectionName = null)
		{
			recoverySettings = recoverySettings ?? AutoRecoverySettings.Off;
			connectionName = connectionName ?? DefaultConnectionName;

			var conn = new Connection();

			try
			{
				await conn
					.Connect(hostname, vhost, username, password, port, connectionName, throwOnError: true)
					.ConfigureAwait(false);

				if (LogAdapter.ExtendedLogEnabled)
					LogAdapter.LogDebug("ConnectionFactory", "Connected to " + hostname + ":" + port);

				return recoverySettings.Enabled ? (IConnection)new RecoveryEnabledConnection(hostname, conn, recoverySettings) : conn;
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