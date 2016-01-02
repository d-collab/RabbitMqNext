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
			var conn = new Connection();
			try
			{
				await conn.Connect("localhost");

				Console.WriteLine("[Connected]");

				conn.Close();
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
