namespace RabbitMqNext.Internals
{
	using System;
	using System.Collections.Concurrent;
	using System.Collections.Generic;
	using System.Runtime.CompilerServices;
	using System.Threading;
	using System.Threading.Tasks;

	// From http://blogs.msdn.com/b/pfxteam/archive/2012/02/11/10266920.aspx


	public class AsyncAutoResetEvent
	{
		private readonly Queue<TaskCompletionSource<bool>> m_waits = new Queue<TaskCompletionSource<bool>>();
		private bool m_signaled;

		public void Set()
		{
			TaskCompletionSource<bool> toRelease = null;
			lock (m_waits)
			{
				if (m_waits.Count > 0)
					toRelease = m_waits.Dequeue();
				else if (!m_signaled)
					m_signaled = true;
			}
			if (toRelease != null)
				toRelease.SetResult(true);
		}

		public Task WaitAsync(CancellationToken token)
		{
			lock (m_waits)
			{
				if (m_signaled)
				{
					m_signaled = false;
					return Task.CompletedTask;
				}
				else
				{
					var tcs = new TaskCompletionSource<bool>(/*TaskCreationOptions.RunContinuationsAsynchronously*/);
					m_waits.Enqueue(tcs);
					return tcs.Task;
				}
			}
		}

		public void Dispose()
		{
		}
	}

	public class AsyncAutoResetCASEvent
	{
		// private static readonly Task s_completed = Task.FromResult(true);
		// private readonly Queue<TaskCompletionSource<bool>> m_waits = new Queue<TaskCompletionSource<bool>>();
		private readonly ConcurrentQueue<TaskCompletionSource<bool>> _waits = new ConcurrentQueue<TaskCompletionSource<bool>>();
		private volatile int _signal1;
		private volatile int _signal2;

		public AsyncAutoResetCASEvent()
		{
		}

		public Task WaitAsync(CancellationToken token)
		{
			while (_waits.IsEmpty && !token.IsCancellationRequested)
			{
				if (_signal1 == 0 && _signal2 == 0) // unset state
					break;

//				var s1 = _signal1;
//				var s2 = _signal2;
				// if (s1 != 0 || s2 != 0) // potential set state
				{
					// transition to unset
					_signal1 -= 1;
					_signal2 -= 2;
					
					// if succeeded, let it go thru
					if (_signal1 == 0 && _signal2 == 0)
					{
						// Console.WriteLine("*");
						return Task.CompletedTask;
					}
				}
			} // loop if something different 

			var tcs = new TaskCompletionSource<bool>(/*TaskCreationOptions.RunContinuationsAsynchronously*/);
			_waits.Enqueue(tcs);
			return tcs.Task;
		}

		public void Set()
		{
			TaskCompletionSource<bool> tcs;
			if (_waits.TryDequeue(out tcs)) 
			{
				tcs.SetResult(true); // let first in line move forward
			}
			else // nobody in line. moves to set state
			{
//				var s1 = _signal1;
//				var s2 = _signal2;
//				Console.Write("_");
				do
				{
					_signal1 = 1;
					_signal2 = 2;
				} while (_signal1 != 1 || _signal2 != 2);
				// Console.WriteLine("Success!");
			}
//			TaskCompletionSource<bool> toRelease = null;
//			lock (m_waits)
//			{
//				if (m_waits.Count > 0)
//					m_waits.Dequeue().SetResult(true);
//				else if (!m_signaled)
//					m_signaled = true;
//			}
//			if (toRelease != null)
//				toRelease.SetResult(true);
		}

		public void Dispose()
		{

		}
	}

	/// <summary>
	/// not really the same semantic as ManualResetEvent 
	/// since only 1 waiter is released per Set. But ideal for single prod/consumer situation
	/// </summary>
	public sealed class AsyncManualResetEvent : IDisposable
	{
//		private volatile TaskCompletionSource<bool> m_tcs = new TaskCompletionSource<bool>();
//
//		public AsyncManualResetEvent()
//		{
//		}
//
//		public Task WaitAsync()
//		{
//			return m_tcs.Task;
//		}
//
//		[MethodImpl(MethodImplOptions.AggressiveInlining)]
//		public void Set2()
//		{
//			m_tcs.TrySetResult(true);
//		}
//
//		[MethodImpl(MethodImplOptions.AggressiveInlining)]
//		public void Set()
//		{
//			var tcs = m_tcs;
//			Task.Factory.StartNew(s => ((TaskCompletionSource<bool>)s).TrySetResult(true),
//				tcs, CancellationToken.None, TaskCreationOptions.AttachedToParent, TaskScheduler.Default);
////			tcs.Task.Wait();
//		}
//
//		[MethodImpl(MethodImplOptions.AggressiveInlining)]
//		public void Reset()
//		{
//			while (true)
//			{
//				var tcs = m_tcs;
//				if (!tcs.Task.IsCompleted ||
//					Interlocked.CompareExchange(ref m_tcs, new TaskCompletionSource<bool>(), tcs) == tcs)
//					return;
//			}
//		}
//
//		public void Dispose()
//		{
//			
//		}

		private readonly SemaphoreSlim _semaphoreSlim;

		public AsyncManualResetEvent(bool initialState = true)
		{
			_semaphoreSlim = new SemaphoreSlim(initialState ? 1 : 0, 1);
		}

		public bool IsSet()
		{
			return _semaphoreSlim.CurrentCount > 0;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public void Set2()
		{
			if (_semaphoreSlim.CurrentCount == 0)
				_semaphoreSlim.Release();
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public Task WaitAsync(CancellationToken cancellationToken)
		{
			return _semaphoreSlim.WaitAsync(cancellationToken);
		}

		public void Dispose()
		{
			_semaphoreSlim.Dispose();
		}
	}
}