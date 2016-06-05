namespace RabbitMqNext.Internals
{
	using System;
	using System.Collections.Concurrent;
	using System.ComponentModel;
	using System.Threading;

	/// <summary>
	/// TODO: implement a faster version. 
	/// all these memory barried ops in a loop are very inneficient.
	/// 
	/// Array based lock-free stack will be better. Watch out for ABA.
	/// </summary>
	public sealed class ObjectPool<T> : IDisposable where T : class 
	{
		private const int DefaultCapacity = 5;

		private readonly Func<T> _objectGenerator;
//		private readonly SemaphoreSlim _semaphore;
		private readonly T[] _array;
		private readonly int _capacity;
		// private readonly ConcurrentQueue<T> _queue;
		
		public ObjectPool(Func<T> objectGenerator, int capacity = DefaultCapacity, bool preInitialize = false)
		{
			if (objectGenerator == null) throw new ArgumentNullException("objectGenerator");

			_capacity = capacity;
//			_semaphore = new SemaphoreSlim(_capacity, _capacity);
			_array = new T[_capacity];
			_objectGenerator = objectGenerator;
			// _queue = new ConcurrentQueue<T>();

			if (preInitialize)
			{
				for (int i = 0; i < _capacity; i++)
				{
					_array[i] = objectGenerator();
					// _queue.Enqueue( objectGenerator() );
//					PutObject(objectGenerator());
				}
			}
		}

//		public T GetObject()
//		{
//			T item;
//			if (_queue.TryDequeue(out item))
//			{
//				var initer = item as ISupportInitialize;
//				if (initer != null) initer.BeginInit();
//				return item;
//			}
//
//			var newObj = _objectGenerator();
//			{
//				var initer = newObj as ISupportInitialize;
//				if (initer != null) initer.BeginInit();
//			}
//			return newObj;
//		}
//
//		public void PutObject(T item)
//		{
//			var disposable = item as IDisposable;
//			if (disposable != null) disposable.Dispose();
//
//			if (_queue.Count < _capacity)
//			{
//				_queue.Enqueue(item);
//			}
//		}

		public T GetObject()
		{
//			_semaphore.Wait();

			for (var i = 0; i < _capacity; i++)
			{
				// worst case scenario is O(n) and cmp exc is slow. so think about better impl
				var v = Interlocked.Exchange(ref _array[i], null); 
				if (v != null)
				{
					// Console.WriteLine("Pool " + typeof(T).Name + " GetObject at index " + i);

					var initer = v as ISupportInitialize;
					if (initer != null) initer.BeginInit();

					return v;
				}
			}

			// Console.WriteLine("Pool " + typeof(T).Name + " run out of items. creating new one ");
			var newObj = _objectGenerator();
			{
				var initer = newObj as ISupportInitialize;
				if (initer != null) initer.BeginInit();
			}
			return newObj;
		}

		public void PutObject(T item)
		{
			var disposable = item as IDisposable;
			if (disposable != null) disposable.Dispose();

			for (int i = 0; i < _capacity; i++)
			{
				// worst case scenario is O(n) and cmp exc is slow. so think about better impl
				var v = Interlocked.CompareExchange(ref _array[i], item, null);
				if (v == null)
				{
					return; // found spot. done
				}
			}

			// Console.WriteLine("Pool " + typeof(T).Name + " PutObject: no empty slots ");

//			_semaphore.Release();
		}

		public void Dispose()
		{
//			_semaphore.Dispose();
		}
	}
}