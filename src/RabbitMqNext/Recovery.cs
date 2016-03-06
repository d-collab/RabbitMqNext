namespace RabbitMqNext
{
	using System.Collections.Generic;

	public class ConnectionRecoveryStrategy 
	{
		public ConnectionRecoveryStrategy(string hostname, string vhost, string username, string password, int port)
		{
		}

		public ConnectionRecoveryStrategy(IEnumerable<string> hostnames, string vhost, string username, string password, int port)
		{
		}

		// void RegisterChannel()
		// void UnregisterChannel()
	}

	public class ChannelRecoveryStrategy
	{
		// void RegisterResource()

	}
}
