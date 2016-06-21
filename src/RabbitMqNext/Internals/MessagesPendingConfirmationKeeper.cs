namespace RabbitMqNext.Internals
{
	using System;
	using System.Runtime.CompilerServices;
	using System.Threading;
	using System.Threading.Tasks;

	/// <summary>
	/// Used when a channel is set up for publish confirm. 
	/// keeps a bounded list of messages pending confirmation, and releases then 
	/// when a ack/nack is received (or shutdown)
	/// </summary>
	internal class MessagesPendingConfirmationKeeper
	{
		private readonly ulong _max;
		private readonly CancellationToken _token;
		private readonly SemaphoreSlim _semaphoreSlim;
		private readonly TaskCompletionSource<bool>[] _tasks;
		
		private ulong _lastSeq = 0;
		private ulong _lastConfirmed = 0;

		public MessagesPendingConfirmationKeeper(int max, CancellationToken token)
		{
			_max = (ulong) max;
			_token = token;
			_tasks = new TaskCompletionSource<bool>[max];
			_semaphoreSlim = new SemaphoreSlim(max, max);
		}

		public ulong Max
		{
			get { return _max; }
		}

		// Ensure there's a spot available. we want to block 
		// at the user api level, not at the frame writing 
		// level which would hog everything
		public void WaitForSemaphore()
		{
			_semaphoreSlim.Wait(_token);
		}

		public void Add(TaskCompletionSource<bool> tcs)
		{
			// happens past semaphore, from the frame writing thread, thus "safe"

			var success = false;

			// for (int i = 0; i < _tasks.Length; i++)
			{
				var id = GetNext();
				var index = id % _max;

				if (Interlocked.CompareExchange(ref _tasks[index], tcs, null) == null)
				{
					success = true;
//					break;
				}
				else
				{
					// Spot in use, so continue up to sizeOf(_tasks) attempts
				}
			}

			if (!success) // probably will never happen due to the semaphore, but just in case. 
			{
				// fail fast, better than leaving the task hanging forever
				tcs.TrySetException(new Exception("MessagesPendingConfirmationKeeper: Could not find free spot for the waiting task"));
			}
		}
		
		// invoked from the read frame thread only. continuations need to be executed async
		public void Confirm(ulong deliveryTag, bool multiple, bool requeue, bool isAck)
		{
			var startPos = multiple ? _lastConfirmed + 1 : deliveryTag;

			for (var i = startPos; i <= deliveryTag; i++)
			{
				var index = deliveryTag % _max;
				// var tcs = _tasks[index];
				var tcs = Interlocked.Exchange(ref _tasks[index], null);

				_semaphoreSlim.Release();

				if (tcs == null) throw new Exception("_tasks is broken!");

				if (isAck)
					tcs.TrySetResult(true);
				else
					tcs.TrySetException(new Exception("Server said it rejected this message. Sorry"));
			}

			_lastConfirmed = deliveryTag;
		}

		public void DrainDueToFailure(AmqpError error)
		{
			for (int i = 0; i < _tasks.Length; i++)
			{
				var tcs = Interlocked.Exchange(ref _tasks[i], null);
				if (tcs == null) continue;

				tcs.TrySetException(new Exception("Cancelled due to error " + error.ToErrorString()));
			}
		}

		public void DrainDueToShutdown()
		{
			for (int i = 0; i < _tasks.Length; i++)
			{
				var tcs = Interlocked.Exchange(ref _tasks[i], null);
				if (tcs == null) continue;

				tcs.TrySetException(new Exception("Cancelled due to shutdown"));
			}
		}

		public void Dispose()
		{
			_semaphoreSlim.Dispose();
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