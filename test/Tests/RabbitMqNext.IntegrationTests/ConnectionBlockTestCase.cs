namespace RabbitMqNext.IntegrationTests
{
	using System;
	using System.Collections.Generic;
	using System.Text;
	using System.Threading.Tasks;
	using NUnit.Framework;

	[TestFixture]
	public class ConnectionBlockTestCase : BaseTest
	{
		[SetUp]
		public void Init()
		{
			LogAdapter.ProtocolLevelLogEnabled = true;
		}

		[TearDown]
		public override void EndTest()
		{
			LogAdapter.ProtocolLevelLogEnabled = false;

			base.EndTest();
		}

		[Test]
		public async Task Exausting_server_should_trigger_connection_block_or_channel_flow()
		{
			Console.WriteLine("Exausting_server_should_trigger_connection_block_or_channel_flow");

			var conn = await base.StartConnection(AutoRecoverySettings.Off);
			var channel = await conn.CreateChannel();
			var eventsReceived = new List<string>();
			var queueName = "queue_to_exhaust";

			conn.ConnectionBlocked += reason =>
			{
				eventsReceived.Add("ConnectionBlocked " + reason);
				channel.QueuePurge(queueName, waitConfirmation: false).IntentionallyNotAwaited();
			};
			conn.ConnectionUnblocked += () =>
			{
				eventsReceived.Add("ConnectionUnblocked ");
			};

			channel.ChannelBlocked += reason =>
			{
				eventsReceived.Add("ChannelBlocked " + reason);
				channel.QueuePurge(queueName, waitConfirmation: false).IntentionallyNotAwaited();
			};
			channel.ChannelUnblocked += () =>
			{
				eventsReceived.Add("ChannelUnblocked ");
			};

			try
			{
				await channel.QueueDeclare(queueName, false, false, false, true, null, waitConfirmation: true);

				for (int i = 0; i < 6000000; i++) 
				{
					channel.BasicPublishFast("", queueName, true, BasicProperties.Empty, 
											 Encoding.UTF8.GetBytes("hello world message to exhaust the server"));
				}
			}
			finally
			{
				channel.QueuePurge(queueName, waitConfirmation: false).IntentionallyNotAwaited();
			}

			foreach (var @event in eventsReceived)
			{
				Console.WriteLine(@event);
			}
		}
	}
}