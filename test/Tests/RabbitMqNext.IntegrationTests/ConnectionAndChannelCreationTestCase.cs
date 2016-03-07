namespace RabbitMqNext.IntegrationTests
{
	using System;
	using System.Threading.Tasks;
	using FluentAssertions;
	using NUnit.Framework;


	[TestFixture]
	public class ConnectionAndChannelCreationTestCase : BaseTest
    {
		[Test, Explicit("Takes too long")]
		public async Task ConnectWithListOfHosts()
		{
			Console.WriteLine(System.Reflection.MethodBase.GetCurrentMethod().Name);

			var badHostsWithGoodAsLast = new []
			{
				"192.168.0.23",
				_host
			};

			var conn = await ConnectionFactory.Connect(badHostsWithGoodAsLast, _vhost, _username, _password);

			conn.Dispose();
		}

		[Test]
		public async Task OpenAndCloseCleanlyUponDispose()
		{
			Console.WriteLine(System.Reflection.MethodBase.GetCurrentMethod().Name);

			IConnection conn = null;
			using (conn = await base.StartConnection())
			{
				conn.IsClosed.Should().BeFalse();
			}
			conn.IsClosed.Should().BeTrue();
		}

		[Test]
		public async Task OpenAndCloseChannelsCleanly()
		{
			Console.WriteLine(System.Reflection.MethodBase.GetCurrentMethod().Name);


			using (var conn = await base.StartConnection())
			{
				var newChannel1 = await conn.CreateChannel();
				var newChannel2 = await conn.CreateChannel();

				newChannel1.ChannelNumber.Should().Be(1);
				newChannel2.ChannelNumber.Should().Be(2);
				newChannel1.IsConfirmationEnabled.Should().BeFalse();
				newChannel2.IsConfirmationEnabled.Should().BeFalse();
				newChannel1.IsClosed.Should().BeFalse();
				newChannel2.IsClosed.Should().BeFalse();

				await newChannel1.Close();
				await newChannel2.Close();

				newChannel1.IsClosed.Should().BeTrue();
				newChannel2.IsClosed.Should().BeTrue();
			}
		}

		[Test]
		public async Task ClosingConnection_Should_CloseAllChannels()
		{
			Console.WriteLine(System.Reflection.MethodBase.GetCurrentMethod().Name);


			var conn = await base.StartConnection();

			var newChannel1 = await conn.CreateChannel();
			var newChannel2 = await conn.CreateChannel();

			newChannel1.ChannelNumber.Should().Be(1);
			newChannel2.ChannelNumber.Should().Be(2);
			newChannel1.IsClosed.Should().BeFalse();
			newChannel2.IsClosed.Should().BeFalse();

			conn.Dispose();

			newChannel1.IsClosed.Should().BeTrue();
			newChannel2.IsClosed.Should().BeTrue();
		}

		[Test]
		public async Task Opens_PubConfirm_Channel()
		{
			Console.WriteLine(System.Reflection.MethodBase.GetCurrentMethod().Name);


			using (var conn = await base.StartConnection())
			{
				var newChannel1 = await conn.CreateChannelWithPublishConfirmation();

				newChannel1.ChannelNumber.Should().Be(1);
				newChannel1.IsClosed.Should().BeFalse();
				newChannel1.IsConfirmationEnabled.Should().BeTrue();

				await newChannel1.Close();

				newChannel1.IsClosed.Should().BeTrue();
			}
		}
    }
}
