namespace RabbitMqNext.Internals.RingBuffer.Locks
{
	using System.Collections.Concurrent;
	using System.Threading;
	using System.Threading.Tasks;

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
}