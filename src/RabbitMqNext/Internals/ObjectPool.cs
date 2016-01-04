namespace RabbitMqNext.Internals
{
	using System;
	using System.Collections.Concurrent;
	using System.Threading;


	public sealed class ObjectPool<T> : IDisposable where T : class 
	{
		private const int Capacity = 5;

		private readonly Func<T> _objectGenerator;
//		private readonly CancellationToken _cancellationToken;

		private readonly SemaphoreSlim _semaphore = new SemaphoreSlim(Capacity, Capacity);
		private readonly T[] _array = new T[Capacity];

		public ObjectPool(Func<T> objectGenerator
				/*, CancellationToken cancellationToken,*/ 
				// , int initialCapacity = 0
			)
		{
			if (objectGenerator == null) throw new ArgumentNullException("objectGenerator");

			_objectGenerator = objectGenerator;
		}

		public T GetObject()
		{
			_semaphore.Wait();

			for (var i = 0; i < Capacity; i++)
			{
				var v = Interlocked.Exchange(ref _array[i], null);
				if (v != null) return v;	
			}
			
			return _objectGenerator();
		}

		public void PutObject(T item)
		{
			var disposable = item as IDisposable;
			if (disposable != null) disposable.Dispose();

			for (int i = 0; i < Capacity; i++)
			{
				var v = Interlocked.CompareExchange(ref _array[i], item, null);
				if (v == null)
				{
					break;
				}
			}

			_semaphore.Release();
		}

		public void Dispose()
		{
			_semaphore.Dispose();
		}

//		public T GetObject()
//		{
//			T item;
//			if (_objects.TryTake(out item)) return item;
//			return _objectGenerator();
//		}
//
//		public void PutObject(T item)
//		{
//			var disposable = item as IDisposable;
//			if (disposable != null)
//				disposable.Dispose();
//
//			_objects.Add(item);
//		}
	}
}