namespace RabbitMqNext.Tests
{
	using System;
	using System.Text;
	using System.Threading;
	using FluentAssertions;
	using Internals.RingBuffer;
	using NUnit.Framework;

	[TestFixture]
	public class RingBuffer2OpsTestCase
	{
		private CancellationTokenSource _cancellationTokenSrc;
		private BufferRingBuffer _buffer;
		private static byte[] SomeBuffer = Encoding.ASCII.GetBytes("The tragedy of the commons is a term, probably coined by the Victorian economist William Forster Lloyd[1] and later used by the ecologist Garrett Hardin, to denote a situation where individuals acting independently and rationally according to each other's self-interest behave contrary to the best interests of the whole by depleting some common resource. The concept was based upon an essay written in 1833 by Lloyd, who used a hypothetical example of the effects of unregulated grazing on common land in the British Isles. This became widely-known over a century later due to an article written by Garrett Hardin in 1968.");
		private byte[] _temp;

		[SetUp]
		public void Init()
		{
			_cancellationTokenSrc = new CancellationTokenSource();
			_buffer = new BufferRingBuffer(_cancellationTokenSrc.Token, 32, new LockWaitingStrategy(_cancellationTokenSrc.Token));

			_temp = new byte[300];
		}

		[TearDown]
		public void TearDown()
		{
			_cancellationTokenSrc.Dispose();	
		}

		[Test]
		public void ClaimWriteRegion_DoesntOverwrite()
		{
			var written = _buffer.Write(SomeBuffer, 0, 30, writeAll: false);
			written.Should().Be(30);

			var read = _buffer.Read(_temp, 0, _temp.Length); // readpos = 30
			read.Should().Be(30);

			written = _buffer.Write(SomeBuffer, 30, 31, writeAll: false);
			written.Should().Be(31);

			read = _buffer.Read(_temp, 0, _temp.Length); // readpos = 61 % 32 = 29
			read.Should().Be(31);

			written = _buffer.Write(SomeBuffer, 0, 31, writeAll: false);  // write pos = 28
			written.Should().Be(31);

			read = _buffer.Read(_temp, 0, 1); // readpos = 30
			read.Should().Be(1);
			read = _buffer.Read(_temp, 0, 1); // readpos = 31
			read.Should().Be(1);
			read = _buffer.Read(_temp, 0, 1); // readpos = 32 % 32 = 0
			read.Should().Be(1);

			written = _buffer.Write(SomeBuffer, 0, 3, writeAll: false);   // write pos = 0
			written.Should().Be(3);
		}

		[Test]
		public void ClaimWriteRegion_DoesntOverwrite2()
		{
			var written = _buffer.Write(SomeBuffer, 0, 30, writeAll: false);
			written.Should().Be(30);

			var read = _buffer.Read(_temp, 0, _temp.Length); // readpos = 30
			read.Should().Be(30);

			written = _buffer.Write(SomeBuffer, 30, 31, writeAll: false);
			written.Should().Be(31);

			read = _buffer.Read(_temp, 0, _temp.Length); // readpos = 61 % 32 = 29
			read.Should().Be(31);

			written = _buffer.Write(SomeBuffer, 0, 31, writeAll: false);  // write pos = 28
			written.Should().Be(31);

			read = _buffer.Read(_temp, 0, 1); // readpos = 30
			read.Should().Be(1);
			read = _buffer.Read(_temp, 0, 1); // readpos = 31
			read.Should().Be(1);
			read = _buffer.Read(_temp, 0, 1); // readpos = 32 % 32 = 0
			read.Should().Be(1);
			read = _buffer.Read(_temp, 0, 1); // readpos = 33 % 32 = 1
			read.Should().Be(1);

			written = _buffer.Write(SomeBuffer, 0, 4, writeAll: false);   // write pos = 0
			written.Should().Be(4);
		}

		[Test]
		public void ClaimWriteRegion_DoesntOverwrite3()
		{
			var written = _buffer.Write(SomeBuffer, 0, 31, writeAll: false);
			written.Should().Be(31);

			var read = _buffer.Read(_temp, 0, 1);
			read.Should().Be(1);

			written = _buffer.Write(SomeBuffer, 0, 1, writeAll: false);
			written.Should().Be(1);

			read = _buffer.Read(_temp, 0, _temp.Length);
			read.Should().Be(31);
		}
	}
}