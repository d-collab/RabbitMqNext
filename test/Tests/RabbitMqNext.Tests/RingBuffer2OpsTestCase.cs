namespace RabbitMqNext.Tests
{
	using System;
	using System.Threading;
	using System.Threading.Tasks;
	using FluentAssertions;
	using Internals.RingBuffer;
	using NUnit.Framework;

	[TestFixture]
	public class RingBuffer2OpsTestCase
	{
		private CancellationTokenSource _cancellationTokenSrc;
		private ByteRingBuffer _buffer;
		private byte[] SomeBuffer;
		private byte[] SourceBuffer;
		private byte[] _temp;
		private Random _rnd;

		[SetUp]
		public void Init()
		{
			_rnd = new Random();
			_cancellationTokenSrc = new CancellationTokenSource();
			_buffer = new ByteRingBuffer(_cancellationTokenSrc.Token, 32);

			_temp = new byte[300];

			SomeBuffer = new byte[200];
			for (int i = 0; i < SomeBuffer.Length; i++)
			{
				SomeBuffer[i] = (byte) (i % 256);
			}

			SourceBuffer = new byte[2048];
			for (int i = 0; i < SourceBuffer.Length; i++)
			{
				SourceBuffer[i] = (byte)(i % 256);
			}
		}

		[TearDown]
		public void TearDown()
		{
			_cancellationTokenSrc.Dispose();	
		}

//		[Test]
//		public void AddReadingGateMax()
//		{
//			ReadingGate dummy;
//			for (int i = 0; i < 64; i++)
//			{
//				_buffer.TryAddReadingGate(1, out dummy).Should().BeTrue();
//			}
//
//			_buffer.TryAddReadingGate(1, out dummy).Should().BeFalse();
//			dummy.Should().BeNull();
//		}
//
//		[Test]
//		public void GetMinGate()
//		{
//			ReadingGate dummy;
//			for (int i = 63; i >= 0; i--)
//			{
//				_buffer._state._readPosition = (uint) i;
//				_buffer.TryAddReadingGate(1, out dummy);
//			}
//			var min = (int?)  _buffer.GetMinReadingGate();
//			min.Should().Be(0);
//		}
//
//		[Test]
//		public void GetMinGateAfterRemovingMin()
//		{
//			ReadingGate last = null;
//			for (int i = 31; i >= 0; i--)
//			{
//				_buffer._state._readPosition = (uint)i;
//				_buffer.TryAddReadingGate(1, out last);
//			}
//			var min = (int)_buffer.GetMinReadingGate();
//			min.Should().Be(0);
//
//			_buffer.RemoveReadingGate(last);
//
//			min = (int)_buffer.GetMinReadingGate();
//			min.Should().Be(1);
//		}
//
//		[Test]
//		public void DoesntOverwriteGatePos()
//		{
//			var written = _buffer.Write(SomeBuffer, 0, 15, writeAll: false);
//			written.Should().Be(15);
//
//			var read = _buffer.Read(_temp, 0, 2); // readpos = 2
//			read.Should().Be(2);
//			_temp.ShouldBeTo(new byte[] { 0, 1 });
//
//			ReadingGate gate;
//			_buffer.TryAddReadingGate(10, out gate);
//			gate.gpos.Should().Be(2);
//
//			read = _buffer.Read(_temp, 0, 10); // readpos = 12 but gate is at 2
//			read.Should().Be(10);
//			_temp.ShouldBeTo(new byte[] { 2, 3,4,5,6,7,8,9,10, 11 });
//
//			written = _buffer.Write(SomeBuffer, 15, 17, writeAll: false);
//			written.Should().Be(17);
//
//			read = _buffer.Read(_temp, 0, 5, false, gate); // reading from gate, at pos 2
//			read.Should().Be(5);
//			gate.gpos.Should().Be(7);
//			_temp.ShouldBeTo(new byte[] { 2, 3, 4, 5, 6,  });
//
//			read = _buffer.Read(_temp, 0, 5, false, gate); // reading from gate, at pos 7
//			read.Should().Be(5);
//			gate.gpos.Should().Be(12);
//			_temp.ShouldBeTo(new byte[] { 7, 8, 9, 10, 11, });
//
//			_buffer.RemoveReadingGate(gate);
//
//			written = _buffer.Write(SomeBuffer, 0, 11, writeAll: false);
//			written.Should().Be(11);
//		}

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

//		[TestCase(true, true)]
//		[TestCase(true, false)]
//		[TestCase(false, false)]
//		public void StressTest(bool writeInPieces, bool withWriteDelays)
//		{
//			var ringBuffer = new ByteRingBuffer(_cancellationTokenSrc.Token, 0x4000);
//			var streamAdapter = new RingBufferStreamAdapter(ringBuffer);
////			int concurrent = 0;
//			
//			const int loops = 1000;
//
//			var writerTask = Task.Run(() =>
//			{
//				for (int i = 0; i < loops; i++)
//				{
//					var payloadSize = _rnd.Next(0, SourceBuffer.Length) + 1;
//					var payloadBuffer = new byte[4];
//					payloadBuffer[0] = 0xFF;
//					payloadBuffer[2] = (byte)((payloadSize & 0xFF00) >> 8);
//					payloadBuffer[1] = (byte)(payloadSize & 0x00FF);
//					payloadBuffer[3] = 0xFF;
//
//					// Console.WriteLine("Wrote " + payloadSize);
//
//					ringBuffer.Write(payloadBuffer, 0, payloadBuffer.Length);
//
//					if (!writeInPieces) // single batch write
//					{
//						ringBuffer.Write(SourceBuffer, 0, payloadSize);
//					}
//					else // in pieces
//					{
//						var totalWritten = 0; 
//						while (totalWritten < payloadSize)
//						{
//							if (withWriteDelays) Thread.Sleep( _rnd.Next(0, 4) + 1 );
//							var toWrite = _rnd.Next(0, payloadSize - totalWritten) + 1;
//							totalWritten += ringBuffer.Write(SourceBuffer, totalWritten, toWrite);
//						}
//					}
//				}
//				// Console.WriteLine("Write completed");
//			});
//
//			var readerTask = Task.Run(() =>
//			{
//				for (int i = 0; i < loops; i++)
//				{
//					if (i % 10 == 0) Thread.Sleep(130); // simulate fake contention 
//
//					var payloadHeader = new byte[4];
//					streamAdapter.Read(payloadHeader, 0, 4, fillBuffer: true);
//
//					if (payloadHeader[0] != 0xFF)
//					{
//						Console.WriteLine("H1 expecting 0XFF but found " + payloadHeader[0]);
//						break;
//					}
//					if (payloadHeader[3] != 0xFF)
//					{
//						Console.WriteLine("H2 expecting 0XFF but found " + payloadHeader[0]);
//						break;
//					}
//					
//					var payloadSize = BitConverter.ToInt16(payloadHeader, 1);
////					Console.WriteLine("Read " + payloadSize);
//
//					var barriedStream = new RingBufferStreamReadBarrier(streamAdapter, payloadSize);
//
//					ringBuffer.Skip(payloadSize);
//
//					ThreadPool.UnsafeQueueUserWorkItem((stream) =>
////					Task.Factory.StartNew((stream) =>
//					{
//						var streamBarried = (RingBufferStreamReadBarrier) stream;
//						try
//						{
//							var payloadBuffer = new byte[2048];
//
//							// Console.WriteLine("Started " + streamBarried.Length + " concurrent " + Interlocked.Increment(ref concurrent));
//
//							var totalread = 0;
//							while (totalread < streamBarried.Length)
//							{
//								var read = streamBarried.Read(payloadBuffer, totalread, payloadBuffer.Length - totalread);
//								totalread += read;
//							}
//
////							Interlocked.Decrement(ref concurrent);
////							Console.WriteLine("Done " + streamBarried.Length + " concurrent " + Interlocked.Decrement(ref concurrent));
//
//							totalread.Should().Be((int)streamBarried.Length);
//
//							var hasErrors = false;
//							for (int j = 0; j < totalread; j++)
//							{
//								var exp = (j%256);
//								if (payloadBuffer[j] != exp)
//								{
//									Console.WriteLine("Expecting " + exp + " but got " + payloadBuffer[j]);
//									hasErrors = true;
//									break;
//								}
//							}
//
////							var output = hasErrors ? Console.Error : Console.Out;
////							output.WriteLine("Done " + (hasErrors ? " with errors " : " successfully "));
//						}
//						finally
//						{
//							streamBarried.Dispose();
//						}
//					}, barriedStream);
//				}
//			});
//
//			Task.WaitAll(writerTask, readerTask);
//
////			Thread.CurrentThread.Join();
//		}
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