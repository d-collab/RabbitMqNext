namespace RabbitMqNext.Internals
{
	using System;
	using System.Runtime.CompilerServices;
	using System.Threading;
	using System.Threading.Tasks;

	// From http://blogs.msdn.com/b/pfxteam/archive/2012/02/11/10266920.aspx

	/// <summary>
	/// not really the same semantic as ManualResetEvent 
	/// since only 1 waiter is released per Set. But ideal for single prod/consumer situation
	/// </summary>
	public sealed class AsyncManualResetEvent : IDisposable
	{
		private volatile TaskCompletionSource<bool> m_tcs = new TaskCompletionSource<bool>();

		public Task WaitAsync() { return m_tcs.Task; }

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public void Set2()
		{
			m_tcs.TrySetResult(true);
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public void Set()
		{
			var tcs = m_tcs;
			Task.Factory.StartNew(s => ((TaskCompletionSource<bool>)s).TrySetResult(true),
				tcs, CancellationToken.None, TaskCreationOptions.AttachedToParent, TaskScheduler.Default);
//			tcs.Task.Wait();
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public void Reset()
		{
			while (true)
			{
				var tcs = m_tcs;
				if (!tcs.Task.IsCompleted ||
					Interlocked.CompareExchange(ref m_tcs, new TaskCompletionSource<bool>(), tcs) == tcs)
					return;
			}
		}

		public void Dispose()
		{
			
		}

//		private readonly SemaphoreSlim _semaphoreSlim;
//
//		public AsyncManualResetEvent(bool initialState = true)
//		{
//			_semaphoreSlim = new SemaphoreSlim(initialState ? 1 : 0, 1);
//		}
//
//		public bool IsSet()
//		{
//			return _semaphoreSlim.CurrentCount > 0;
//		}
//
//		[MethodImpl(MethodImplOptions.AggressiveInlining)]
//		public void Set()
//		{
//			if (_semaphoreSlim.CurrentCount == 0)
//				_semaphoreSlim.Release();
//		}
//
//		[MethodImpl(MethodImplOptions.AggressiveInlining)]
//		public Task WaitAsync()
//		{
//			return _semaphoreSlim.WaitAsync();
//		}
//
//		public void Dispose()
//		{
//			_semaphoreSlim.Dispose();
//		}
	}
}