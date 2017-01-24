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
		private const string LogSource = "MessagesPendingConfirmationKeeper";

		private readonly ulong _max;
		private readonly CancellationToken _token;
		private readonly SemaphoreSlim _semaphoreSlim;
		private readonly TaskCompletionSource<bool>[] _tasks;
		
		private ulong _lastSeq = 0; // no need to be volatile
		private ulong _lastConfirmed = 0; // ditto

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
			// happens past semaphore, from the frame writing thread, thus safe
			
			var id = ++_lastSeq;
			var index = id % _max;

			if (Interlocked.CompareExchange(ref _tasks[index], tcs, null) != null)
			{
				// should never happen
				tcs.TrySetException(new Exception("ConfirmationKeeper: Could not find free spot for the waiting task"));
			}

			if (LogAdapter.IsDebugEnabled)
				LogAdapter.LogDebug(LogSource, string.Format("Confirm: took " + index));
		}
		
		// invoked from the read frame thread only. continuations need to be executed async
		public void Confirm(ulong deliveryTag, bool multiple, bool requeue, bool isAck)
		{
			if (LogAdapter.IsDebugEnabled)
				LogAdapter.LogDebug(LogSource, string.Format("Confirming delivery {0} lastConfirmed {4}  M: {1} Reque: {2} isAck: {3}", deliveryTag, multiple, requeue, isAck, _lastConfirmed));

			var startPos = multiple ? _lastConfirmed + 1 : deliveryTag;

			for (var i = startPos; i <= deliveryTag; i++)
			{
				var index = i % _max;

				var tcs = Interlocked.Exchange(ref _tasks[index], null);

				_semaphoreSlim.Release();

				if (tcs == null)
				{
					LogAdapter.LogError(LogSource, "Unexpected tcs null at " + index);
				}
				else
				{
					if (isAck)
						tcs.TrySetResult(true);
					else
						tcs.TrySetException(new Exception("Server said it rejected this message. Sorry"));
				}
			}

			_lastConfirmed = deliveryTag;
		}

		public void DrainDueToFailure(AmqpError error)
		{
			Exception exception = null;
			for (int i = 0; i < _tasks.Length; i++)
			{
				var tcs = Interlocked.Exchange(ref _tasks[i], null);
				if (tcs == null) continue;

				if (exception == null)
					exception = new Exception("Cancelled due to error " + error.ToErrorString());

				tcs.TrySetException(exception);
			}
		}

		public void DrainDueToShutdown()
		{
			Exception exception = null;
			for (int i = 0; i < _tasks.Length; i++)
			{
				var tcs = Interlocked.Exchange(ref _tasks[i], null);
				if (tcs == null) continue;

				if (exception == null)
					exception = new Exception("Cancelled due to shutdown");

				tcs.TrySetException(exception);
			}
		}

		public void Dispose()
		{
			_semaphoreSlim.Dispose();
		}
	}
}