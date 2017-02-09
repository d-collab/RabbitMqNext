namespace RabbitMqNext.Tests
{
	using System;
	using System.ComponentModel;
	using System.Runtime.CompilerServices;
	using System.Threading;
	using Internals;
	using Internals.RingBuffer;
	using NUnit.Framework;

	[TestFixture]
	public class ObjectPoolArrayTestCase
	{
		private static readonly Random _rnd = new Random();

		private ObjectPoolArray<FakeRecycleableObj> _pool;

		[TestCase(1, 200)]
		[TestCase(2, 200)]
		[TestCase(3, 200)]
		[TestCase(4, 200)]
		[TestCase(5, 200)]
		[TestCase(6, 200)]
		[TestCase(7, 200)]
		[TestCase(8, 200)]
		public void ConsistencyCheck(int howManyThreads, int poolSize)
		{
			var iterations = 100000;

			_pool = new ObjectPoolArray<FakeRecycleableObj>(
				() => new FakeRecycleableObj(i => _pool.PutObject(i)), 
				poolSize, preInitialize: true, ignoreDispose: false);

			var countDown = new CountdownEvent(howManyThreads);
			var hasError = false;

			for (int i = 0; i < howManyThreads; i++)
			{
				ThreadFactory.BackgroundThread(() =>
				{
					try
					{
						for (int j = 0; j < iterations; j++)
						{
							var obj = _pool.GetObject();
							if (i % 20 == 0)
								Thread.Sleep(_rnd.Next(1, 3)); // some entropy
							obj.Use("something"); // Use will recycle itself
						}
					}
					catch (Exception ex)
					{
						Console.Error.WriteLine(ex);
						hasError = true;
					}
					finally
					{
						countDown.Signal();
					}

				}, "Test_" + i);
			}

			countDown.Wait();

			_pool.DumpDiagnostics();

			Assert.False(hasError, "Not expecting to have error(s)");
		}


		class FakeRecycleableObj : ISupportInitialize, IDisposable
		{
			private readonly Action<FakeRecycleableObj> _recycler;
			public string SomePointer;

			private int _isInUse;

			private const int InUseConst = 1;
			private const int NotInUseConst = 0;

			public FakeRecycleableObj(Action<FakeRecycleableObj> recycler)
			{
				_recycler = recycler;
			}

			public void Use(string someRandStr)
			{
				AssertCanBeUsed();

				this.SomePointer = someRandStr;

				AssertCanBeUsed();

				_recycler(this);
			}

			public void BeginInit()
			{
				if (Interlocked.CompareExchange(ref _isInUse, value: InUseConst, comparand: NotInUseConst) != NotInUseConst)
					throw new Exception("FakeRecycleableObj being shared inadvertently 2");
			}

			public void EndInit()
			{
			}

			public void Dispose()
			{
				this.SomePointer = null;

				if (Interlocked.CompareExchange(ref _isInUse, value: NotInUseConst, comparand: InUseConst) != InUseConst)
					throw new Exception("FakeRecycleableObj being shared inadvertently 1");
			}

			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			private void AssertCanBeUsed()
			{
				if (Volatile.Read(ref _isInUse) != InUseConst)
					throw new Exception("Cannot use a recycled obj");
			}
		}	
	}
}