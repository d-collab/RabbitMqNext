namespace RabbitMqNext
{
	using System;
	using System.Threading;
	using System.Threading.Tasks;
	using Internals;

	public abstract class BaseRpcHelper<T> : BaseRpcHelper, IDisposable
	{
		protected readonly ConsumeMode _mode;
		protected readonly Timer _timeoutTimer;
		protected readonly int? _timeoutInMs;
		protected readonly long? _timeoutInTicks;
		protected readonly TaskSlim<T>[] _pendingCalls;
		protected readonly ObjectPool<TaskSlim<T>> _taskResultPool;

		protected volatile uint _correlationCounter;
		protected volatile bool _disposed;

		protected AmqpQueueInfo _replyQueueName;
		protected string _subscription;


		protected BaseRpcHelper(Channel channel, int maxConcurrentCalls, ConsumeMode mode, int? timeoutInMs)
			: base(maxConcurrentCalls)
		{
			_channel = channel;
			_mode = mode;
			_timeoutInMs = timeoutInMs;

			// the impl keeps a timer pool so this is light and efficient
			if (timeoutInMs.HasValue)
			{
				_timeoutInTicks = timeoutInMs * TimeSpan.TicksPerMillisecond;
				_timeoutTimer = new System.Threading.Timer(OnTimeoutCheck, null, timeoutInMs.Value, timeoutInMs.Value);
			}

			_pendingCalls = new TaskSlim<T>[maxConcurrentCalls];
			_taskResultPool = new ObjectPool<TaskSlim<T>>(() =>
				new TaskSlim<T>((inst) => _taskResultPool.PutObject(inst)), maxConcurrentCalls, preInitialize: true);
		}

		public void Dispose()
		{
			if (_disposed) return;
			Thread.MemoryBarrier();
			_disposed = true;

			if (_timeoutTimer != null)
			{
				this._timeoutTimer.Dispose();
			}

			if (!string.IsNullOrEmpty(_subscription))
			{
				try
				{
					_channel.BasicCancel(_subscription, false).Wait();
				}
				catch (Exception)
				{
					// no problem!
				}
			}

			DrainPendingCalls();
		}

		protected async Task Setup()
		{
			_replyQueueName = await _channel.QueueDeclare("", // temp
				false, false, exclusive: true, autoDelete: true,
				waitConfirmation: true, arguments: null).ConfigureAwait(false);

			_subscription = await _channel.BasicConsume(_mode, OnReplyReceived, _replyQueueName.Name,
				consumerTag: "",
				withoutAcks: true, exclusive: true, arguments: null, waitConfirmation: true).ConfigureAwait(false);
		}

		protected abstract Task OnReplyReceived(MessageDelivery delivery);
		
		protected bool SecureSpotAndUniqueCorrelationId(TaskSlim<T> task, out long pos, out uint correlationId)
		{
			correlationId = 0;
			pos = 0L;

			var tries = 0;
			while (tries++ < _maxConcurrentCalls)
			{
				var correlationIndex = _correlationCounter++;
				pos = correlationIndex % _maxConcurrentCalls;

				if (Interlocked.CompareExchange(ref _pendingCalls[pos], task, null) == null)
				{
					correlationId = correlationIndex;
					return true;
				}
			}

			return false;
		}

		protected override void DrainPendingCalls()
		{
			for (int i = 0; i < _pendingCalls.Length; i++)
			{
				var pendingCall = Interlocked.Exchange(ref _pendingCalls[i], null);
				if (pendingCall == null) continue;

				try
				{
					// this races with OnReplyReceived. 
					pendingCall.SetException(new Exception("Cancelled due to shutdown"), runContinuationAsync: true);

					// and we want just one call to Release
					_semaphoreSlim.Release();
				}
				catch (Exception)
				{
					// race situation. ignores
				}
			}
		}

		private void OnTimeoutCheck(object state)
		{
			var now = DateTime.Now.Ticks;

			foreach (var pendingCall in _pendingCalls)
			{
				if (pendingCall == null) continue;
				if (now - pendingCall.Started > _timeoutInTicks)
				{
					try
					{
						pendingCall.SetException(new Exception("Rpc call timeout"), runContinuationAsync: true);
					}
					catch (Exception)
					{
					}
				}
			}
		}
	}
}