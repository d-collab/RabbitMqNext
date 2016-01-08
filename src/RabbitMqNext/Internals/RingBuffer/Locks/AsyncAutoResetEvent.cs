namespace RabbitMqNext.Internals.RingBuffer.Locks
{
	using System.Collections.Generic;
	using System.Threading;
	using System.Threading.Tasks;

	public class AsyncAutoResetEvent
	{
		private readonly Queue<TaskCompletionSource<bool>> m_waits = new Queue<TaskCompletionSource<bool>>();
		private bool m_signaled;

		public void Set()
		{
//			TaskCompletionSource<bool> toRelease = null;
			lock (m_waits)
			{
				if (m_waits.Count > 0)
				{
					while (m_waits.Count != 0)
					{
						m_waits.Dequeue().SetResult(true);	
					}
					// toRelease = m_waits.Dequeue();
					// m_waits.Dequeue().SetResult(true);
				}
				else if (!m_signaled)
					m_signaled = true;
			}
//			if (toRelease != null)
//				toRelease.SetResult(true);
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
					var tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
					m_waits.Enqueue(tcs);
					return tcs.Task;
				}
			}
		}

		public void Dispose()
		{
		}
	}
}