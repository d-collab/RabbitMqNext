namespace RabbitMqNext.Tests
{
	using FluentAssertions;
	using Internals.RingBuffer;
	using NUnit.Framework;

	[TestFixture]
	public class UtilsTestCase
	{
		[TestCase(1, false)]
		[TestCase(2, true)]
		[TestCase(4, true)]
		[TestCase(524288, true)]
		[TestCase(524287, false)]
		public void IsPowerOfTwoTest(int value, bool expectedResult)
		{
			Utils.IsPowerOfTwo(value).Should().Be(expectedResult);
		}
	}
}
