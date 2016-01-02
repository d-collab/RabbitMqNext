namespace RabbitMqNext.Tests
{
	using System;
	using System.Collections.Generic;
	using System.Linq;
	using System.Threading.Tasks;
	using FluentAssertions;
	using Internals;
	using NUnit.Framework;

	[TestFixture]
	public class RingBufferStreamTestCase 
    {
		private static readonly Random _rnd  = new Random();

		[Test]
		public async Task AllAtOnce()
		{
			var t1 = Task.Factory.StartNew(() => GC_min_allocation_test(), TaskCreationOptions.LongRunning);
			var t2 = Task.Factory.StartNew(() => GC_min_allocation_test(), TaskCreationOptions.LongRunning);
			var t3 = Task.Factory.StartNew(() => GC_min_allocation_test(), TaskCreationOptions.LongRunning);
			var t4 = Task.Factory.StartNew(() => GC_min_allocation_test(), TaskCreationOptions.LongRunning);

			Task.WaitAll(t1, t2, t3, t4);
		}

		/// <summary>
		/// Causes a lot of potential wraps and contention
		/// </summary>
		[Test]
		public async Task Fast_producer_slow_consumer()
		{
			Console.WriteLine("Fast_producer_slow_consumer");

			var rbuffer = new RingBufferStream();
			bool done = false;

			const int mod = 37;

			var producerTask = Task.Run(async () =>
			{
				for (int i = 0; i < 1024*1000; i += 10)
				{
					var buffer = new []
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

					rbuffer.Insert(buffer, 0, buffer.Length);
				}

				done = true;
			});

			var consumed = new List<byte>(capacity: 1024 * 1000);

			var consumerTask = Task.Run(async () =>
			{
				int totalRead = 0;
				while (true)
				{
					await Task.Delay(_rnd.Next(10));

//					Console.WriteLine("will read...");
					var temp = new byte[199];
					var read = rbuffer.Read(temp, 0, temp.Length);
					totalRead += read;

					if (read == 0) continue;

					for (int i = 0; i < read; i++)
					{
						consumed.Add(temp[i]);
					}

					// Console.WriteLine("[Dump] read: " + read + " total " + totalRead);// + " [" + temp.Aggregate("", (s, b) => s + b + ", ") + "]");

					if (done && rbuffer.Position == rbuffer.Length)
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
		public async Task Insert_1_byte_by_1()
		{
			Console.WriteLine("Insert_1_byte_by_1");

			var rbuffer = new RingBufferStream();

			int expectedLast = 0;

			for (int i = 0; i < 1024 * 100; i++)
			{
				rbuffer.Insert(new[] { (byte)(i % 256) }, 0, 1);

				if (i % 99 == 0)
				{
					var temp = new byte[101];
					var read = rbuffer.Read(temp, 0, temp.Length);

					Console.WriteLine("[Dump] read: " + read + " [" + temp.Aggregate("", (s, b) => s + b + ", ") + "]");

					if (i == 0)
					{
						read.Should().Be(1);
						temp[read - 1].Should().Be((byte)expectedLast);
					}
					else
					{
						read.Should().Be(99);
						temp[read - 1].Should().Be((byte)expectedLast);
					}
					expectedLast += 99;
					expectedLast %= 256;
				}
			}

			{
				var temp = new byte[101];
				var read = rbuffer.Read(temp, 0, temp.Length);
				Console.WriteLine("[Final] read: " + read + " [" + temp.Aggregate("", (s, b) => s + b + ", ") + "]");
			}
		}

		[Test]
		public async Task Variable_size_writes_slow_consumer()
		{
			Console.WriteLine("Variable_size_writes_slow_consumer");

			var rbuffer = new RingBufferStream();
			bool done = false;

			const int mod = 37;
			const int sizeOfSet = 1024*1000;

			var producerTask = Task.Run(async () =>
			{
				int stepAndBufferSize = 0;
				for (int i = 0; i < sizeOfSet; i += stepAndBufferSize)
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
						(byte) ((i + 10) %mod),
						(byte) ((i + 11) %mod),
						(byte) ((i + 12) %mod),
						(byte) ((i + 13) %mod),
						(byte) ((i + 14) %mod),
						(byte) ((i + 15) %mod),
						(byte) ((i + 16) %mod),
						(byte) ((i + 17) %mod),
						(byte) ((i + 18) %mod),
						(byte) ((i + 19) %mod),
						(byte) ((i + 20) %mod),
						(byte) ((i + 21) %mod),
						(byte) ((i + 22) %mod),
						(byte) ((i + 23) %mod),
						(byte) ((i + 24) %mod),
						(byte) ((i + 25) %mod),
						(byte) ((i + 26) %mod),
						(byte) ((i + 27) %mod),
						(byte) ((i + 28) %mod),
						(byte) ((i + 29) %mod),
					};

					stepAndBufferSize = _rnd.Next(buffer.Length);

					rbuffer.Insert(buffer, 0, stepAndBufferSize);
				}

				done = true;
			});

			var consumed = new List<byte>(capacity: sizeOfSet);

			var consumerTask = Task.Run(async () =>
			{
				int totalRead = 0;
				while (true)
				{
					await Task.Delay(_rnd.Next(10));

					// Console.WriteLine("will read...");
					var temp = new byte[400];

					var readSize = _rnd.Next(temp.Length - 1) + 1;

					var read = rbuffer.Read(temp, 0, readSize);
					totalRead += read;

					if (read == 0) continue;

					for (int i = 0; i < read; i++)
					{
						consumed.Add(temp[i]);
					}

//					Console.WriteLine("[Dump] read: " + read + " total " + totalRead);// + " [" + temp.Aggregate("", (s, b) => s + b + ", ") + "]");

					if (done && rbuffer.Position == rbuffer.Length)
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
		public async Task GC_min_allocation_test()
		{
			Console.WriteLine("GC_min_allocation_test Starting...");
//			await Task.Delay(3000);

			Console.WriteLine("Started");

			var rbuffer = new RingBufferStream();
			bool done = false;

			const int mod = 37;
			var producerBuffer = new byte[10];

			var producerTask = Task.Run(async () =>
			{
				for (int i = 0; i < 1000000000000000000; i += 10)
				{
					producerBuffer[0] = (byte) (i % mod);
					producerBuffer[1] = (byte) ((i + 1) % mod);
					producerBuffer[2] = (byte) ((i + 2) % mod);
					producerBuffer[3] = (byte) ((i + 3) % mod);
					producerBuffer[4] = (byte) ((i + 4) % mod);
					producerBuffer[5] = (byte) ((i + 5) % mod);
					producerBuffer[6] = (byte) ((i + 6) % mod);
					producerBuffer[7] = (byte) ((i + 7) % mod);
					producerBuffer[8] = (byte) ((i + 8) % mod);
					producerBuffer[9] = (byte) ((i + 9) % mod);

					rbuffer.Insert(producerBuffer, 0, producerBuffer.Length);
				}

				done = true;
			});

			var temp = new byte[199];

			var consumerTask = Task.Run(async () =>
			{
				int totalRead = 0;
				while (true)
				{
//					await Task.Delay(_rnd.Next(10));

					// Console.WriteLine("will read...");
					
					var read = rbuffer.Read(temp, 0, temp.Length);
					totalRead += read;

					if (read == 0) continue;

					// Console.WriteLine("[Dump] read: " + read + " total " + totalRead);// + " [" + temp.Aggregate("", (s, b) => s + b + ", ") + "]");

					if (done && rbuffer.Position == rbuffer.Length)
					{
						break;
					}
				}
				Console.WriteLine("Done");
			});

			Task.WaitAll(producerTask, consumerTask);

			Console.WriteLine("Checking consistency...");

			Console.WriteLine("Completed");
		}
    }
}
