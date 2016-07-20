namespace RabbitMqNext.Internals
{
	using System;
	using System.Collections.Concurrent;
	using System.ComponentModel;
	using System.Diagnostics.Contracts;
	using System.Runtime.InteropServices;


	/// <summary>
	/// TODO: implement a faster version. 
	/// all these memory barried ops in a loop are very inneficient.
	/// 
	/// Array based lock-free stack will be better. Watch out for ABA.
	/// </summary>
	public sealed class ObjectPool<T> where T : class 
	{
		private const int DefaultCapacity = 5;

		private readonly Func<T> _objectGenerator;
//		private readonly SemaphoreSlim _semaphore;
//		private readonly T[] _array;
		private readonly int _capacity;
		private readonly bool _ignoreDispose;
		private readonly ConcurrentStack<T> _queue;

		public ObjectPool(Func<T> objectGenerator, int capacity = DefaultCapacity, 
							bool preInitialize = false, bool ignoreDispose = false)
		{
			if (objectGenerator == null) throw new ArgumentNullException("objectGenerator");

			_capacity = capacity;
			_ignoreDispose = ignoreDispose;
//			_semaphore = new SemaphoreSlim(_capacity, _capacity);
//			_array = new T[_capacity];
			_objectGenerator = objectGenerator;
			_queue = new ConcurrentStack<T>();

			if (preInitialize)
			{
				for (int i = 0; i < _capacity; i++)
				{
//					_array[i] = objectGenerator();
					_queue.Push( objectGenerator() );
//					PutObject(objectGenerator());
				}
			}
		}

		public T GetObject()
		{
			T item;
			if (_queue.TryPop(out item))
			{
				var initer = item as ISupportInitialize;
				if (initer != null) initer.BeginInit();
				return item;
			}

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
			if (!_ignoreDispose && disposable != null) disposable.Dispose();

			if (_queue.Count < _capacity)
			{
				_queue.Push(item);
			}
		}
	}
}