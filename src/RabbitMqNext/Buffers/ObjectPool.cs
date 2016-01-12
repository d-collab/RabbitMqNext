namespace RabbitMqNext.Internals
{
	using System;
	using System.Threading;

	/// <summary>
	/// TODO: implement a faster version. 
	/// all these memory barried ops in a loop are very inneficient
	/// </summary>
	public sealed class ObjectPool<T> : IDisposable where T : class 
	{
//		public class ObjectPoolItem<T>
//		{
//			public T Item;
//			public int index;
//		}

		private const int DefaultCapacity = 5;

		private readonly Func<T> _objectGenerator;
//		private readonly SemaphoreSlim _semaphore;
		private readonly T[] _array;
		private readonly int _capacity;

		public ObjectPool(Func<T> objectGenerator, int capacity = DefaultCapacity, bool preInitialize = false)
		{
			if (objectGenerator == null) throw new ArgumentNullException("objectGenerator");

			_capacity = capacity;
//			_semaphore = new SemaphoreSlim(_capacity, _capacity);
			_array = new T[_capacity];
			_objectGenerator = objectGenerator;

			if (preInitialize)
			{
				for (int i = 0; i < _capacity; i++)
				{
					_array[i] = objectGenerator();
				}
			}
		}

		public T GetObject()
		{
//			_semaphore.Wait();

			for (var i = 0; i < _capacity; i++)
			{
				var v = Interlocked.Exchange(ref _array[i], null);
				if (v != null)
				{
					// Console.WriteLine("Pool " + typeof(T).Name + " GetObject at index " + i);
					return v;
				}
			}

			Console.WriteLine("Pool " + typeof(T).Name + " run out of items. creating new one ");
			return _objectGenerator();
		}

		public void PutObject(T item)
		{
			var disposable = item as IDisposable;
			if (disposable != null) disposable.Dispose();

			for (int i = 0; i < _capacity; i++)
			{
				var v = Interlocked.CompareExchange(ref _array[i], item, null);
				if (v == null)
				{
					// Console.WriteLine("Pool " + typeof(T).Name + " PutObject at index " + i);
					// break;
					return;
				}
			}

			Console.WriteLine("Pool " + typeof(T).Name + " PutObject: no empty slots ");

//			_semaphore.Release();
		}

		public void Dispose()
		{
//			_semaphore.Dispose();
		}
	}
}