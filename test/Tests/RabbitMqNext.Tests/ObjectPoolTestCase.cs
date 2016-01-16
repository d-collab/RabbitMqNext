namespace RabbitMqNext.Tests
{
	using System;
	using FluentAssertions;
	using Internals;
	using NUnit.Framework;

	[TestFixture]
	public class ObjectPoolTestCase
	{
		[Test]
		public void PutRecyclesObj()
		{
			var pool = new ObjectPool<DisposedTracking>(() => new DisposedTracking());

			var item1 = pool.GetObject();
			item1.Use();

			pool.PutObject(item1);

			item1.Disposed.Should().BeTrue();

		}

		[Test]
		public void GetPut()
		{
			var pool = new ObjectPool<DisposedTracking>(() => new DisposedTracking());

			var item1 = pool.GetObject();
			item1.Use();

			var item2 = pool.GetObject();
			item2.Use();

			pool.PutObject(item2);
			pool.PutObject(item1);
			
			var item3 = pool.GetObject();
			item3.Used.Should().BeTrue();

			var item4 = pool.GetObject();
			item4.Used.Should().BeTrue();
		}

		class DisposedTracking : IDisposable
		{
			public bool Used = false;
			public bool Disposed;

			public void Use()
			{
				Used = true;
				Disposed = false;
			}

			public void Dispose()
			{
				Disposed = true;
			}
		}
	}

}
