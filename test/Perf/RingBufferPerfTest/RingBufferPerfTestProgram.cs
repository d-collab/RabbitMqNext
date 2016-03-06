namespace RingBufferPerfTest
{
	using System;
	using System.Diagnostics;
	using System.Runtime;
	using System.Threading;
	using System.Threading.Tasks;
	using RabbitMqNext.Internals.RingBuffer;

	class RingBufferPerfTestProgram
	{

		static void Main(string[] args)
		{
			Console.WriteLine("Is Server GC: " + GCSettings.IsServerGC);
			GCSettings.LargeObjectHeapCompactionMode = GCLargeObjectHeapCompactionMode.CompactOnce;
			Console.WriteLine("Compaction mode: " + GCSettings.LargeObjectHeapCompactionMode);
			Console.WriteLine("Latency mode: " + GCSettings.LatencyMode);
			GCSettings.LatencyMode = GCLatencyMode.SustainedLowLatency;
			Console.WriteLine("New Latency mode: " + GCSettings.LatencyMode);



			var ringBuffer = new ByteRingBuffer();

			const int mod = 37;
			var producerBuffer = new byte[256];
			for (int j = 0; j < producerBuffer.Length; j++)
			{
				producerBuffer[j] = (byte)(j % mod);
			}

			ulong iterations = 50000000L;
			ulong totalExpected = (ulong)(iterations * (ulong)producerBuffer.Length);

			var watch = Stopwatch.StartNew();

			var producerTask = Task.Factory.StartNew(() =>
			{
				ulong totalWritten = 0L;

				for (ulong i = 0L; i < iterations; i += 1)
				{
					ringBuffer.Write(producerBuffer, 0, producerBuffer.Length);
					totalWritten += (ulong)producerBuffer.Length;
				}

				Console.WriteLine("Done: written " + totalWritten);
			});

			var temp = new byte[2048];

			var consumerTask = Task.Factory.StartNew(() =>
			{
				ulong totalRead = 0L;
				while (true)
				{
					var read = ringBuffer.Read(temp, 0, temp.Length);
					totalRead += (ulong)read;

					if (totalRead == totalExpected)
					{
						break;
					}
				}
				Console.WriteLine("Done too. Read " + totalRead);
			});

			Task.WaitAll(producerTask, consumerTask);

			Console.WriteLine("Completed in " + watch.Elapsed.TotalMilliseconds + "ms");
		}
	}
}
