namespace RabbitMqNext.Tests
{
	using System;
	using System.Threading;
	using FluentAssertions;
	using Io;
	using NUnit.Framework;


	[TestFixture]
	public class ChannelOpsTestCase
	{
		private Channel _channel;

		[SetUp]
		public void Init()
		{
			_channel = new Channel(0, new ConnectionIO(new Connection()), new CancellationToken());
		}

		[Test]
		public void BasicProperties_Rent_Should_return_clean_usable_instance()
		{
			// Act
			var prop = _channel.RentBasicProperties();

			// Assert
			prop.IsFrozen.Should().BeFalse();
			prop.IsRecycled.Should().BeFalse();
			prop.IsReusable.Should().BeTrue();
		}

		[Test]
		public void BasicProperties_Return_should_mark_instance()
		{
			// Arrange
			var prop = _channel.RentBasicProperties();

			// Act
			_channel.Return(prop);

			// Assert
			prop.IsFrozen.Should().BeTrue();
			prop.IsRecycled.Should().BeTrue();
			prop.IsReusable.Should().BeTrue();
		}

		[Test]
		public void BasicProperties_When_returning_more_than_once_same_instance_Should_Throw()
		{
			// Arrange
			var prop = _channel.RentBasicProperties();

			// Act & Assert
			_channel.Return(prop);

			Assert.That(() => _channel.Return(prop), Throws.Exception);
		}

		[Test]
		public void BasicProperties_When_using_returned_instance_Should_Throw()
		{
			// Arrange
			var prop = _channel.RentBasicProperties();
			_channel.Return(prop);

			// Act & Assert
			Assert.Throws<Exception>(() => prop.Type = "test");
			Assert.Throws<Exception>(() =>
			{
				var dum = prop.Headers.Count;
			} );
		}
	}
}