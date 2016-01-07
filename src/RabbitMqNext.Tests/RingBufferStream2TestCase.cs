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
	public class RingBufferStream2TestCase
	{
		private static readonly Random _rnd = new Random();

		[Test]
		public void Basic()
		{
			var token = new CancellationToken();
			var ringBuffer = new RingBufferStream2(token, 32, new LockWaitingStrategy(token));

			var producer = new SingleProducer(ringBuffer, token);
			var originalBuf = new byte[] {1, 2, 3, 4, 5, 6, 7, 8};
			producer.Write(originalBuf, 0, originalBuf.Length);

			var consumer = new SingleConsumer(ringBuffer, token);
			var otherBuf = new byte[1000];
			var totalRead = consumer.Read(otherBuf, 0, otherBuf.Length);
		}

		[Test]
		public void Insert_1_byte_by_1()
		{
			Console.WriteLine("Insert_1_byte_by_1");

			const int BufferSize = 0x800;

			var cancelationTokenSource = new CancellationTokenSource();
			var ringBuffer = new RingBufferStream2(cancelationTokenSource.Token,
				BufferSize, 
				new LockWaitingStrategy(cancelationTokenSource.Token));

			var producer = new SingleProducer(ringBuffer, cancelationTokenSource.Token);
			var consumer = new SingleConsumer(ringBuffer, cancelationTokenSource.Token);

			int expectedLast = 0;
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
			var rbuffer = new RingBufferStream2(token, BufferSize, new LockWaitingStrategy(token));

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

			// const int BufferSize = 2100000000;
			const int BufferSize = 0x8000000;
			// const int BufferSize = 1024 * 60;

			var token = new CancellationToken();
			// var rbuffer = new RingBufferStream2(token, BufferSize, new SpinLockWaitingStrategy(token));
			var rbuffer = new RingBufferStream2(token, BufferSize, new LockWaitingStrategy(token));
			var producer = new SingleProducer(rbuffer, token);
			var consumer = new SingleConsumer(rbuffer, token);


			const int mod = 37;
			var producerBuffer = new byte[256];
			for (int j = 0; j < producerBuffer.Length; j++)
			{
				producerBuffer[j] = (byte)(j % mod);
			}

			var watch = Stopwatch.StartNew();

			var iterations = 1000000;

			var consumed = new List<byte>(capacity: iterations * producerBuffer.Length);

			var producerTask = Task.Run(() =>
			{
				for (var i = 0L; i < iterations; i += 1)
				{
					producer.Write(producerBuffer, 0, producerBuffer.Length);
				}
			});

			var temp = new byte[2048 * 20];

			var consumerTask = Task.Run(() =>
			{
				int totalRead = 0;
				while (true)
				{
					var read = consumer.Read(temp, 0, temp.Length);
					totalRead += read;

					for (int i = 0; i < read; i++)
					{
						consumed.Add(temp[i]);
					}

					// Console.WriteLine("R " + read + "\t\t" + totalRead);
					// if (read == 0) continue;

					if (totalRead == iterations * producerBuffer.Length)
					{
						break;
					}
				}
				Console.WriteLine("Done");
			});

			Task.WaitAll(producerTask, consumerTask);
			watch.Stop();

			Console.WriteLine("Checking consistency...");

			var x = 0;
			for (int i = 0; i < consumed.Count; i++, x++)
			{
				var v = consumed[i];
				var isValid = v == (x % producerBuffer.Length) % mod;
				if (!isValid)
					Console.WriteLine("Expecting index at " + i + " to be " + (i % mod) + " but was " + v);
			}

			Console.WriteLine("Completed in " + watch.Elapsed.TotalMilliseconds + "ms");
		}
	}
}
