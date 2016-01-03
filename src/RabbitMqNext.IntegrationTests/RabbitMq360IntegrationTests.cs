namespace RabbitMqNext.IntegrationTests
{
	using System;
	using System.Threading;
	using System.Threading.Tasks;
	using NUnit.Framework;

	[TestFixture]
    public class RabbitMq360IntegrationTests
    {
		[Test]
		public async Task Init()
		{
			try
			{
				var conn = await new ConnectionFactory().Connect("localhost");

				Console.WriteLine("[Connected]");

				var newChannel = await conn.CreateChannel();

				Console.WriteLine("[channel created] " + newChannel.ChannelNumber);
			}
			catch (AggregateException ex)
			{
				Console.WriteLine("[Captured error] " + ex.Flatten().Message);
			}
			catch (Exception ex)
			{
				Console.WriteLine("[Captured error 2] " + ex.Message);
			}

			// Thread.CurrentThread.Join(TimeSpan.FromSeconds(50));
		}
    }
}
