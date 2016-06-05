namespace ObjPoolPerfTest
{
	using System;
	using System.Diagnostics;
	using System.Runtime;
	using System.Threading;
	using System.Threading.Tasks;
	using RabbitMqNext.Internals;

	class Program
	{
		private static Random rnd = new Random();

		static void Main(string[] args)
		{
			Console.WriteLine("Is Server GC: " + GCSettings.IsServerGC);
			GCSettings.LargeObjectHeapCompactionMode = GCLargeObjectHeapCompactionMode.CompactOnce;
			Console.WriteLine("Compaction mode: " + GCSettings.LargeObjectHeapCompactionMode);
			Console.WriteLine("Latency mode: " + GCSettings.LatencyMode);
			GCSettings.LatencyMode = GCLatencyMode.SustainedLowLatency;
			Console.WriteLine("New Latency mode: " + GCSettings.LatencyMode);

			var pool = new ObjectPool<FakeItem>(() => new FakeItem(), 1000, true);

			var watch = Stopwatch.StartNew();

			const int Iterations = 10000000;

			Action c = () =>
			{
				for (int i = 0; i < Iterations; i++)
				{
					var obj = pool.GetObject();
					// Thread.SpinWait(rnd.Next(10) + 1);
					Thread.SpinWait(4);
					pool.PutObject(obj);
				}
			};

			var t1 = Task.Factory.StartNew(c);
			var t2 = Task.Factory.StartNew(c);
			var t3 = Task.Factory.StartNew(c);
			var t4 = Task.Factory.StartNew(c);
			var t5 = Task.Factory.StartNew(c);
			var t6 = Task.Factory.StartNew(c);

			Task.WaitAll(t1, t2, t3, t4, t5, t6);

			Console.WriteLine("Completed in " + watch.Elapsed.TotalMilliseconds + "ms");
		}

		class FakeItem : IDisposable
		{
			public void Dispose()
			{
			}
		}
	}

	
}
