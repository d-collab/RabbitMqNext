namespace RabbitMqNext.Tests
{
	using System;
	using System.Collections.Generic;
	using System.Diagnostics;
	using System.Linq;
	using System.Threading;
	using System.Threading.Tasks;
	using FluentAssertions;
	using Internals.RingBuffer;
	using NUnit.Framework;


	[TestFixture]
	public class RingBuffer2TestCase
	{
		private static readonly Random _rnd = new Random();

		[Test]
		public void WritesOf31ReadsOf31_BufferOf32()
		{
			var token = new CancellationToken();
			var ringBuffer = new RingBuffer2(token, 32, new LockWaitingStrategy(token));
			var producer = new SingleProducer(ringBuffer, token);
			var consumer = new SingleConsumer(ringBuffer, token);

			var input = new byte[31];
			var output = new byte[31];
			for (int j = 0; j < input.Length; j++)
			{
				input[j] = (byte)(j % 256);
			}

			for (ulong i = 0L; i < 1431655765 + 32; i++)
			{
				producer.Write(input, 0, input.Length);

				var read = consumer.Read(output, 0, output.Length, fillBuffer: true);
				read.Should().Be(output.Length);

				for (int x = 0; x < output.Length; x++)
				{
					output[x].Should().Be((byte)(x % 256));
				}

				if (i % 1000000 == 0)
				{
					Console.WriteLine("Iteration " + i);
				}
			}
		}

		[Test]
		public void WritesOf3ReadsOf9_BufferOf32()
		{
			var token = new CancellationToken();
			var ringBuffer = new RingBuffer2(token, 32, new LockWaitingStrategy(token));
			var producer = new SingleProducer(ringBuffer, token);
			var consumer = new SingleConsumer(ringBuffer, token);

			var input  = new byte[3];
			var output = new byte[9];

			for (ulong i = 0L; i < 1431655765 + 32; i++)
			{
				input[0] = (byte) (i % 256);
				input[1] = (byte) (i + 1 % 256);
				input[2] = (byte) (i + 2 % 256);
				producer.Write(input, 0, input.Length);
	
				input[0] = (byte) (i + 3 % 256);
				input[1] = (byte) (i + 4 % 256);
				input[2] = (byte) (i + 5 % 256);
				producer.Write(input, 0, input.Length);
	
				input[0] = (byte) (i + 6 % 256);
				input[1] = (byte) (i + 7 % 256);
				input[2] = (byte) (i + 8 % 256);
				producer.Write(input, 0, input.Length);
	
				var read = consumer.Read(output, 0, output.Length, fillBuffer: true);
				read.Should().Be(output.Length);
	
				output[0].Should().Be((byte)(i + 0 % 256));
				output[1].Should().Be((byte)(i + 1 % 256));
				output[2].Should().Be((byte)(i + 2 % 256));
				output[3].Should().Be((byte)(i + 3 % 256));
				output[4].Should().Be((byte)(i + 4 % 256));
				output[5].Should().Be((byte)(i + 5 % 256));
				output[6].Should().Be((byte)(i + 6 % 256));
				output[7].Should().Be((byte)(i + 7 % 256));
				output[8].Should().Be((byte)(i + 8 % 256));

				if (i%1000000 == 0)
				{
					Console.WriteLine("Iteration " + i);
				}
			}
		}

		[Test]
		public void Basic()
		{
			var token = new CancellationToken();
			var ringBuffer = new RingBuffer2(token, 32, new LockWaitingStrategy(token));

			ringBuffer.GlobalReadPos.Should().Be(0);
			ringBuffer.GlobalWritePos.Should().Be(0);
			ringBuffer.ReadPos.Should().Be(0);
			ringBuffer.WritePos.Should().Be(0);

			var producer = new SingleProducer(ringBuffer, token);
			var originalBuf = new byte[] { 0, 1, 2, 3, 4, 5, 6 };
			producer.Write(originalBuf, 0, originalBuf.Length);

			ringBuffer.GlobalReadPos.Should().Be(0);
			ringBuffer.GlobalWritePos.Should().Be(7);
			ringBuffer.ReadPos.Should().Be(0);
			ringBuffer.WritePos.Should().Be(7);

			var consumer = new SingleConsumer(ringBuffer, token);
			var otherBuf = new byte[32];
			var totalRead = consumer.Read(otherBuf, 0, otherBuf.Length, fillBuffer: false);
			totalRead.Should().Be(originalBuf.Length);

			ringBuffer.GlobalReadPos.Should().Be(7);
			ringBuffer.GlobalWritePos.Should().Be(7);
			ringBuffer.ReadPos.Should().Be(7);
			ringBuffer.WritePos.Should().Be(7);

			totalRead = consumer.Read(otherBuf, 0, otherBuf.Length, fillBuffer: false);
			totalRead.Should().Be(0);
			ringBuffer.GlobalReadPos.Should().Be(7);
			ringBuffer.GlobalWritePos.Should().Be(7);
			ringBuffer.ReadPos.Should().Be(7);
			ringBuffer.WritePos.Should().Be(7);

			producer.Write(originalBuf, 0, originalBuf.Length);
			ringBuffer.GlobalReadPos.Should().Be(7);
			ringBuffer.GlobalWritePos.Should().Be(14);
			ringBuffer.ReadPos.Should().Be(7);
			ringBuffer.WritePos.Should().Be(14);

			producer.Write(originalBuf, 0, originalBuf.Length);
			ringBuffer.GlobalReadPos.Should().Be(7);
			ringBuffer.GlobalWritePos.Should().Be(21);
			ringBuffer.ReadPos.Should().Be(7);
			ringBuffer.WritePos.Should().Be(21);

			producer.Write(originalBuf, 0, originalBuf.Length);
			ringBuffer.GlobalReadPos.Should().Be(7);
			ringBuffer.GlobalWritePos.Should().Be(28);
			ringBuffer.ReadPos.Should().Be(7);
			ringBuffer.WritePos.Should().Be(28);

			producer.Write(originalBuf, 0, originalBuf.Length);
			ringBuffer.GlobalReadPos.Should().Be(7);
			ringBuffer.GlobalWritePos.Should().Be(35);
			ringBuffer.ReadPos.Should().Be(7);
			ringBuffer.WritePos.Should().Be(3);

			// writer wrap
			producer.Write(new byte[] { 120,119,118 }, 0, 3);
			ringBuffer.GlobalReadPos.Should().Be(7);
			ringBuffer.GlobalWritePos.Should().Be(38);
			ringBuffer.ReadPos.Should().Be(7);
			ringBuffer.WritePos.Should().Be(6);

			// read whole thing
			totalRead = consumer.Read(otherBuf, 0, otherBuf.Length - 1, fillBuffer: true);
			totalRead.Should().Be(otherBuf.Length - 1);
			ringBuffer.GlobalReadPos.Should().Be(38);
			ringBuffer.GlobalWritePos.Should().Be(38);
			ringBuffer.ReadPos.Should().Be(6);
			ringBuffer.WritePos.Should().Be(6);

			for (ulong i = 0L; i < 1024 * 120; i++)
			{
				var v = (byte) (i%256);
				originalBuf[0] = v;
				producer.Write(originalBuf, 0, 1);
				var read = consumer.Read(otherBuf, 0, 2, fillBuffer: false);
				read.Should().Be(1);
				otherBuf[0].Should().Be(v);
			}
			
		}

		[Test]
		public void Insert_1_byte_by_1()
		{
			Console.WriteLine("Insert_1_byte_by_1");

			const int BufferSize = 0x800;

			var cancelationTokenSource = new CancellationTokenSource();
			var ringBuffer = new RingBuffer2(cancelationTokenSource.Token,
				BufferSize, 
				new LockWaitingStrategy(cancelationTokenSource.Token));

			var producer = new SingleProducer(ringBuffer, cancelationTokenSource.Token);
			var consumer = new SingleConsumer(ringBuffer, cancelationTokenSource.Token);

//			int expectedLast = 0;
			int totalInBuffer = 0;

			for (int i = 0; i < 1024 * 100; i++)
			{
				producer.Write(new[] { (byte)(i % 256) }, 0, 1);
				totalInBuffer += 1;

				if (totalInBuffer == 100)
				{
					var temp = new byte[totalInBuffer];
					var read = consumer.Read(temp, 0, temp.Length, fillBuffer: true);

					totalInBuffer = 0;

					Console.WriteLine(i + "  [Dump] read: " + read + " [" + temp.Aggregate("", (s, b) => s + b + ", ") + "]");
				}
			}
		}

		[Test]
		public void Fast_producer_slow_consumer()
		{
			Console.WriteLine("Fast_producer_slow_consumer");

			const int TotalInserts = 1024*1000;
			const int BufferSize = 0x800000;

			var token = new CancellationToken();
			var rbuffer = new RingBuffer2(token, BufferSize, new LockWaitingStrategy(token));
			// var rbuffer = new RingBuffer2(token, BufferSize, new SpinLockWaitingStrategy(token));

			var producer = new SingleProducer(rbuffer, token);
			var consumer = new SingleConsumer(rbuffer, token);

			const int mod = 37;

			var producerTask = Task.Run(() =>
			{
				for (int i = 0; i < TotalInserts; i += 10)
				{
					var buffer = new[]
					{
						(byte) (i%mod), 
						(byte) ((i + 1) %mod),
						(byte) ((i + 2) %mod),
						(byte) ((i + 3) %mod),
						(byte) ((i + 4) %mod),
						(byte) ((i + 5) %mod),
						(byte) ((i + 6) %mod),
						(byte) ((i + 7) %mod),
						(byte) ((i + 8) %mod),
						(byte) ((i + 9) %mod),
					};

					producer.Write(buffer, 0, buffer.Length);
				}
			});

			var consumed = new List<byte>(capacity: TotalInserts);

			var consumerTask = Task.Run(async () =>
			{
				int totalRead = 0;
				while (true)
				{
					await Task.Delay(_rnd.Next(10));

					// Console.WriteLine("will read...");
					var temp = new byte[199];
					var read = consumer.Read(temp, 0, temp.Length);
					totalRead += read;

					if (read == 0) continue;

					for (int i = 0; i < read; i++)
					{
						consumed.Add(temp[i]);
					}

					// Console.WriteLine("[Dump] read: " + read + " total " + totalRead);// + " [" + temp.Aggregate("", (s, b) => s + b + ", ") + "]");
					// Console.WriteLine("Read \t" + read + " \t" + DateTime.Now.Ticks);

					// if ( done && rbuffer.Position == rbuffer.Length)
					if (consumed.Count == TotalInserts * 10)
					{
						break;
					}
				}
				Console.WriteLine("Done");
			});

			Task.WaitAll(producerTask, consumerTask);

			Console.WriteLine("Checking consistency...");

			for (int i = 0; i < consumed.Count; i++)
			{
				var isValid = consumed[i] == i % mod;
				isValid.Should().BeTrue();
			}

			Console.WriteLine("Completed");
		}

		[Test]
		public void GC_min_allocation_test()
		{
			Console.WriteLine("GC_min_allocation_test Starting...");
			
			Console.WriteLine("Started");

			const int BufferSize = 0x8000000;

			var token = new CancellationToken();
			var rbuffer = new RingBuffer2(token, BufferSize, new LockWaitingStrategy(token));
			var producer = new SingleProducer(rbuffer, token);
			var consumer = new SingleConsumer(rbuffer, token);

			const int mod = 37;
			var producerBuffer = new byte[256];
			for (int j = 0; j < producerBuffer.Length; j++)
			{
				producerBuffer[j] = (byte)(j % mod);
			}

			var watch = Stopwatch.StartNew();

			ulong iterations = 10000000L;
			// 1000000000 old
			// 2560000000 new
			ulong totalExpected = (ulong)(iterations * (ulong)producerBuffer.Length);

			var producerTask = Task.Run(() =>
			{
				ulong totalWritten = 0L;

				for (ulong i = 0L; i < iterations; i += 1)
				{
					producer.Write(producerBuffer, 0, producerBuffer.Length);
					totalWritten += (ulong)producerBuffer.Length;
				}

				Console.WriteLine("Done: written " + totalWritten);
			});

			var temp = new byte[2048];

			var consumerTask = Task.Run(() =>
			{
				ulong totalRead = 0L;
				while (true)
				{
					var read = consumer.Read(temp, 0, temp.Length);
					totalRead += (ulong)read;

					if (totalRead == totalExpected)
					{
						break;
					}
				}
				Console.WriteLine("Done too. Read " + totalRead);
			});

			Task.WaitAll(producerTask, consumerTask);
			watch.Stop();
			{
				var read = consumer.Read(temp, 0, temp.Length);
			}

			Console.WriteLine("Checking consistency...");

			Console.WriteLine("Completed in " + watch.Elapsed.TotalMilliseconds + "ms");
		}
	}
}
