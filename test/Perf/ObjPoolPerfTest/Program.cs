namespace ObjPoolPerfTest
{
	using System;
	using System.Diagnostics;
	using System.Runtime;
	using System.Threading;
	using System.Threading.Tasks;
	using BenchmarkDotNet.Attributes;
	using BenchmarkDotNet.Running;
	using RabbitMqNext.Internals;

	public class Program
	{
		private static Random rnd = new Random();

		private ObjectPool<FakeItem> _objectPool;
		private ObjectPoolArray<FakeItem> _objectPoolArray;

		public Program()
		{
			_objectPool = new ObjectPool<FakeItem>(() => new FakeItem(), 100, true);
			_objectPoolArray = new ObjectPoolArray<FakeItem>(() => new FakeItem(), 100, true);
		}

		static void Main(string[] args)
		{
			Console.WriteLine("Is Server GC: " + GCSettings.IsServerGC);
			GCSettings.LargeObjectHeapCompactionMode = GCLargeObjectHeapCompactionMode.CompactOnce;
			Console.WriteLine("Compaction mode: " + GCSettings.LargeObjectHeapCompactionMode);
			Console.WriteLine("Latency mode: " + GCSettings.LatencyMode);
			GCSettings.LatencyMode = GCLatencyMode.SustainedLowLatency;
			Console.WriteLine("New Latency mode: " + GCSettings.LatencyMode);

			BenchmarkRunner.Run<Program>();

//			var pool = new ObjectPool<FakeItem>(() => new FakeItem(), 1000, true);
//
//			var watch = Stopwatch.StartNew();
//
//			const int Iterations = 10000000;
//
//			Action c = () =>
//			{
//				for (int i = 0; i < Iterations; i++)
//				{
//					var obj = pool.GetObject();
//					// Thread.SpinWait(rnd.Next(10) + 1);
//					Thread.SpinWait(4);
//					pool.PutObject(obj);
//				}
//			};
//
//			var t1 = Task.Factory.StartNew(c);
//			var t2 = Task.Factory.StartNew(c);
//			var t3 = Task.Factory.StartNew(c);
//			var t4 = Task.Factory.StartNew(c);
//			var t5 = Task.Factory.StartNew(c);
//			var t6 = Task.Factory.StartNew(c);
//
//			Task.WaitAll(t1, t2, t3, t4, t5, t6);
//
//			Console.WriteLine("Completed in " + watch.Elapsed.TotalMilliseconds + "ms");
		}

		[Benchmark(Baseline = true)]
		public void VersionWithQueue()
		{
			var item1 = _objectPool.GetObject();
			var item2 = _objectPool.GetObject();
			var item3 = _objectPool.GetObject();
			_objectPool.PutObject(item1);
			var item4 = _objectPool.GetObject();
			var item5 = _objectPool.GetObject();
			var item6 = _objectPool.GetObject();
			var item7 = _objectPool.GetObject();
			var item8 = _objectPool.GetObject();
			var item9 = _objectPool.GetObject();

			_objectPool.PutObject(item2);
			_objectPool.PutObject(item3);
			_objectPool.PutObject(item4);
			_objectPool.PutObject(item5);
			_objectPool.PutObject(item6);
			_objectPool.PutObject(item7);
			_objectPool.PutObject(item8);
			_objectPool.PutObject(item9);
		}

		[Benchmark]
		public void VersionWithArray()
		{
			var item1 = _objectPoolArray.GetObject();
			var item2 = _objectPoolArray.GetObject();
			var item3 = _objectPoolArray.GetObject();
			_objectPoolArray.PutObject(item1);
			var item4 = _objectPoolArray.GetObject();
			var item5 = _objectPoolArray.GetObject();
			var item6 = _objectPoolArray.GetObject();
			var item7 = _objectPoolArray.GetObject();
			var item8 = _objectPoolArray.GetObject();
			var item9 = _objectPoolArray.GetObject();

			_objectPoolArray.PutObject(item2);
			_objectPoolArray.PutObject(item3);
			_objectPoolArray.PutObject(item4);
			_objectPoolArray.PutObject(item5);
			_objectPoolArray.PutObject(item6);
			_objectPoolArray.PutObject(item7);
			_objectPoolArray.PutObject(item8);
			_objectPoolArray.PutObject(item9);
		}

		class FakeItem : IDisposable
		{
			public void Dispose()
			{
			}
		}
	}
}
