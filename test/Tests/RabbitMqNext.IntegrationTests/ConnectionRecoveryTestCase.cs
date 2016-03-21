namespace RabbitMqNext.IntegrationTests
{
	using System;
	using System.Threading.Tasks;
	using FluentAssertions;
	using NUnit.Framework;

	[TestFixture]
	public class ConnectionRecoveryTestCase : BaseTest
	{
		[Test, Ignore("needs review")]
		public async Task ServerErrorShouldTriggerReconnect()
		{
			Console.WriteLine("ServerErrorShouldTriggerReconnect");

			var conn = await base.StartConnection(autoRecovery: true);

			_io._socketHolder.Writer.Write(123456);
			_io._socketHolder.Writer.Write(123456);
			_io._socketHolder.Writer.Write(123456);
			_io._socketHolder.Writer.Write(123456);

			await Task.Delay(1000);
		}
	}
}