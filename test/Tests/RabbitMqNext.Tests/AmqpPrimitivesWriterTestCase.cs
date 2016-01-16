namespace RabbitMqNext.Tests
{
	using System;
	using System.Collections.Generic;
	using FluentAssertions;
	using Internals;
	using NUnit.Framework;

	[TestFixture]
	public class AmqpPrimitivesWriterTestCase
	{
		[Test]
		public void WritesAll()
		{
			var list = new List<ArraySegment<byte>>();

			var writer = new InternalBigEndianWriter((b, off, c) =>
			{
				// _sharedBuffer.Insert(b, off, c);
				list.Add(new ArraySegment<byte>(b,off, c));
			});

			writer.Write((byte)1);
			writer.Write(1.2);
			writer.Write(3.2f);
			writer.Write(10);

			list.Should().HaveCount(4);
		}

		[Test]
		public void WritesInnerBuffers()
		{
			var list = new List<ArraySegment<byte>>();

			var writer = new InternalBigEndianWriter((b, off, c) =>
			{
				// _sharedBuffer.Insert(b, off, c);
				list.Add(new ArraySegment<byte>(b, off, c));
			});

			var temp = new byte[1024];
			temp[0] = 1;
			temp[1] = 2;
			temp[2] = 3;
			temp[3] = 4;

			writer.Write(temp, 0, 3);

			list.Should().HaveCount(1);
			list[0].Count.Should().Be(3);
			list[0].Offset.Should().Be(0);
			list[0].Array.ShouldAllBeEquivalentTo(temp);
		}
	}	
}