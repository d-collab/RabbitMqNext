namespace RabbitMqNext.Internals
{
	using System;
	using System.Collections.Concurrent;

	public class ObjectPool<T>
	{
		// replace by ringbuffer with max capacity (bounded!)
		private readonly ConcurrentBag<T> _objects;
		private readonly Func<T> _objectGenerator;

		public ObjectPool(Func<T> objectGenerator, int initialCapacity = 0)
		{
			if (objectGenerator == null) throw new ArgumentNullException("objectGenerator");
			_objects = new ConcurrentBag<T>();
			_objectGenerator = objectGenerator;

			for (int i = 0; i < initialCapacity; i++)
			{
				PutObject(objectGenerator());
			}
		}

		public T GetObject()
		{
			T item;
			if (_objects.TryTake(out item)) return item;
			return _objectGenerator();
		}

		public void PutObject(T item)
		{
			var disposable = item as IDisposable;
			if (disposable != null)
				disposable.Dispose();

			_objects.Add(item);
		}
	}
}