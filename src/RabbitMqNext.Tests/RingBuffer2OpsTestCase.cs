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
		private byte[] SomeBuffer;
		private byte[] _temp;

		[SetUp]
		public void Init()
		{
			_cancellationTokenSrc = new CancellationTokenSource();
			_buffer = new BufferRingBuffer(_cancellationTokenSrc.Token, 32, new LockWaitingStrategy(_cancellationTokenSrc.Token));

			_temp = new byte[300];

			SomeBuffer = new byte[200];
			for (int i = 0; i < SomeBuffer.Length; i++)
			{
				SomeBuffer[i] = (byte) (i % 256);
			}
		}

		[TearDown]
		public void TearDown()
		{
			_cancellationTokenSrc.Dispose();	
		}

		[Test]
		public void AddReadingGateMax()
		{
			for (int i = 0; i < 32; i++)
			{
				_buffer.AddReadingGate();
			}
			try
			{
				_buffer.AddReadingGate();
				Assert.Fail("should have max'ed out");
			}
			catch
			{
				// expected
			}
		}

		[Test]
		public void GetMinGate()
		{
			for (int i = 31; i >= 0; i--)
			{
				_buffer._readPosition = (uint) i;
				_buffer.AddReadingGate();
			}
			var min = (int)  _buffer.GetMinReadingGate();
			min.Should().Be(0);
		}

		[Test]
		public void GetMinGateAfterRemovingMin()
		{
			ReadingGate last = null;
			for (int i = 31; i >= 0; i--)
			{
				_buffer._readPosition = (uint)i;
				last = _buffer.AddReadingGate();
			}
			var min = (int)_buffer.GetMinReadingGate();
			min.Should().Be(0);

			_buffer.RemoveReadingGate(last);

			min = (int)_buffer.GetMinReadingGate();
			min.Should().Be(1);
		}

		[Test]
		public void DoesntOverwriteGatePos()
		{
			var written = _buffer.Write(SomeBuffer, 0, 15, writeAll: false);
			written.Should().Be(15);

			var read = _buffer.Read(_temp, 0, 2); // readpos = 2
			read.Should().Be(2);
			_temp.ShouldBeTo(new byte[] { 0, 1 });

			var gate = _buffer.AddReadingGate();
			gate.pos.Should().Be(2);

			read = _buffer.Read(_temp, 0, 10); // readpos = 12 but gate is at 2
			read.Should().Be(10);
			_temp.ShouldBeTo(new byte[] { 2, 3,4,5,6,7,8,9,10, 11 });

			written = _buffer.Write(SomeBuffer, 15, 17, writeAll: false);
			written.Should().Be(17);

			read = _buffer.Read(_temp, 0, 5, false, gate); // reading from gate, at pos 2
			read.Should().Be(5);
			gate.pos.Should().Be(7);
			_temp.ShouldBeTo(new byte[] { 2, 3, 4, 5, 6,  });

			read = _buffer.Read(_temp, 0, 5, false, gate); // reading from gate, at pos 7
			read.Should().Be(5);
			gate.pos.Should().Be(12);
			_temp.ShouldBeTo(new byte[] { 7, 8, 9, 10, 11, });

			_buffer.RemoveReadingGate(gate);

			written = _buffer.Write(SomeBuffer, 0, 11, writeAll: false);
			written.Should().Be(11);
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

	static class AssertionExts
	{
		public static void ShouldBeTo(this byte[] source, byte[] expected)
		{
			for (int i = 0; i < expected.Length; i++)
			{
				if (source[i] != expected[i])
					throw new AssertionException("Differ at index " + i + ". Expecting " + expected[i] + " but got " + source[i]);
			}
		}
	}
}