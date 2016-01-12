namespace RabbitMqNext
{
	using System;
	using System.Runtime.CompilerServices;
	using System.Threading;

	internal class MessagesPendingConfirmationKeeper
	{
		private readonly ulong _max;
		private readonly CancellationToken _token;
		private readonly SemaphoreSlim _semaphoreSlim;
		private readonly TaskLight[] _tasks;
		
		private ulong _lastSeq = 0;
		private ulong _lastConfirmed = 0;

		public MessagesPendingConfirmationKeeper(int max, CancellationToken token)
		{
			_max = (ulong) max;
			_token = token;
			_tasks = new TaskLight[max];
			_semaphoreSlim = new SemaphoreSlim(max, max);
		}

		// Ensure there's a spot available. we want to block 
		// at the user api level, not at the frame writing 
		// level which would hog everything
		public void WaitForSemaphore()
		{
			_semaphoreSlim.Wait(_token);
		}

		public void Add(TaskLight tcs)
		{
			// happens past semaphore, from the frame writing thread, thus "safe"

			var id = GetNext();
			var index = id % _max;
			_tasks[index] = tcs;
		}
		
		// invoked from the read frame thread only. continuations need to be executed async
		public void Confirm(ulong deliveryTag, bool multiple, bool requeue, bool isAck)
		{
			var startPos = multiple ? _lastConfirmed + 1 : deliveryTag;

			for (var i = startPos; i <= deliveryTag; i++)
			{
				var index = deliveryTag % _max;
				var tcs = _tasks[index];
				if (tcs == null) throw new Exception("_tasks is broken!");
				_tasks[index] = null;

				if (isAck)
					tcs.SetCompleted(runContinuationAsync: true);
				else 
					tcs.SetException(new Exception("Server said it rejected this message. Sorry"), runContinuationAsync: true);
			}

			_lastConfirmed = deliveryTag;
		}

		public void DrainDueToFailure(string error)
		{
			throw new NotImplementedException();
		}

		public void DrainDueToShutdown()
		{
			throw new NotImplementedException();
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		private ulong GetNext()
		{
			// max is 9,223,372,036,854,775,807 so we're "safe" in pragmatic terms
			// return Interlocked.Increment(ref LastSeq);
			return ++_lastSeq; // safe since invoked from a single thread
		}
	}
}