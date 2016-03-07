namespace RabbitMqNext.IntegrationTests
{
	using System;
	using System.Threading.Tasks;
	using FluentAssertions;
	using NUnit.Framework;

	[TestFixture]
	public class ConnectionRecoveryTestCase : BaseTest
	{
		
		[Test]
		public async Task ServerErrorShouldTriggerReconnect()
		{
			Console.WriteLine(System.Reflection.MethodBase.GetCurrentMethod().Name);

			var conn = await base.StartConnection(autoRecovery: true);

			conn._io._socketHolder.Writer.Write(123456);
			conn._io._socketHolder.Writer.Write(123456);
//			conn._io._socketHolder.Writer.Write(123456);
//			conn._io._socketHolder.Writer.Write(123456);

			await Task.Delay(1000);
		}
	}
}