namespace RabbitMqNext.Internals
{
	using System;
	using System.ComponentModel;
	using System.Threading;

	public sealed class ObjectPoolArray<T> where T : class 
	{
		private const int DefaultCapacity = 5;

		private readonly Func<T> _objectGenerator;
		private readonly T[] _array;
		private readonly int _capacity;
		private readonly bool _ignoreDispose;

		public ObjectPoolArray(Func<T> objectGenerator, int capacity = DefaultCapacity, 
			bool preInitialize = false, bool ignoreDispose = false)
		{
			if (objectGenerator == null) throw new ArgumentNullException("objectGenerator");

			_capacity = capacity;
			_ignoreDispose = ignoreDispose;
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
			if (!_ignoreDispose && disposable != null) disposable.Dispose();

			for (int i = 0; i < _capacity; i++)
			{
				// worst case scenario is O(n) and cmp exc is slow. so think about better impl
				var v = Interlocked.CompareExchange(ref _array[i], item, null);
				if (v == null)
				{
					return; // found spot. done
				}
			}
		}
	}
}