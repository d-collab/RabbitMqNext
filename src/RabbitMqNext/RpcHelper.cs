namespace RabbitMqNext
{
	using System;
	using System.Threading;
	using System.Threading.Tasks;
	using Internals;

	public class RpcHelper : IDisposable
	{
		private readonly Channel _channel;
		private readonly int _maxConcurrentCalls;
		private readonly ConsumeMode _mode;
		private volatile uint _correlationCounter;
		private volatile bool _disposed;
		private AmqpQueueInfo _replyQueueName;
		private string _subscription;

		private readonly ObjectPool<TaskSlim<MessageDelivery>> _taskResultPool;
		private readonly TaskSlim<MessageDelivery>[] _pendingCalls;
		private readonly Timer _timeoutTimer;
		private readonly SemaphoreSlim _semaphoreSlim;
		private readonly int? _timeoutInMs;
		private readonly long? _timeoutInTicks;

		private RpcHelper(Channel channel, int maxConcurrentCalls, ConsumeMode mode, int? timeoutInMs = 6000)
		{
			if (maxConcurrentCalls <= 0) throw new ArgumentOutOfRangeException("maxConcurrentCalls");

			_channel = channel;
			_maxConcurrentCalls = maxConcurrentCalls;
			_mode = mode;
			_timeoutInMs = timeoutInMs;

			_semaphoreSlim = new SemaphoreSlim(maxConcurrentCalls, maxConcurrentCalls);

			// the impl keeps a timer pool so this is light and efficient
			if (timeoutInMs.HasValue)
			{
				_timeoutInTicks = timeoutInMs * TimeSpan.TicksPerMillisecond;
				_timeoutTimer = new System.Threading.Timer(OnTimeoutCheck, null, timeoutInMs.Value, timeoutInMs.Value);
			}

			_pendingCalls = new TaskSlim<MessageDelivery>[maxConcurrentCalls];
			_taskResultPool = new ObjectPool<TaskSlim<MessageDelivery>>(() =>
				new TaskSlim<MessageDelivery>((inst) => _taskResultPool.PutObject(inst)), maxConcurrentCalls, true);
		}

	    public static Task<RpcHelper> Create(Channel channel, int maxConcurrentCalls, ConsumeMode mode,
	        int? timeoutInMs = 6000)
	    {
	        return new RpcHelper(channel, maxConcurrentCalls, mode, timeoutInMs).Setup();
	    }

		private async Task<RpcHelper> Setup()
		{
			_replyQueueName = await _channel.QueueDeclare("", // temp
			    false, false, exclusive: true, autoDelete: true, 
			    waitConfirmation: true, arguments: null).ConfigureAwait(false);

			_subscription = await _channel.BasicConsume(_mode, OnReplyReceived, _replyQueueName.Name, 
			    consumerTag: "", 
			    withoutAcks: true, exclusive: true, arguments: null, waitConfirmation: true).ConfigureAwait(false);

		    return this;
		}

		private Task OnReplyReceived(MessageDelivery delivery)
		{
			var correlationIndex = UInt32.Parse(delivery.properties.CorrelationId);
			var pos = correlationIndex % _maxConcurrentCalls;

			var taskLight = Interlocked.Exchange(ref _pendingCalls[pos], null);

			if (taskLight == null || taskLight.Id != correlationIndex)
			{
				// the helper was disposed and the task list was drained.
				if (_disposed) return Task.CompletedTask;
				
				// other situation, the call timeout'ed previously
			}
			else
			{
				taskLight.SetResult(delivery);
			}

			_semaphoreSlim.Release();

			return Task.CompletedTask;
		}

		public TaskSlim<MessageDelivery> Call(string exchange, string routing, BasicProperties properties, ArraySegment<byte> buffer)
		{
			_semaphoreSlim.Wait();

			var task = _taskResultPool.GetObject();

			uint correlationId;
			if (!SecureSpotAndUniqueCorrelationId(task, out correlationId))
			{
				_semaphoreSlim.Release();

				Console.WriteLine("max calls reached!");
				task.SetException(new Exception("reached max calls"));
				return task;
			}

			task.Id = correlationId; // so we can confirm we have the right instance later
			task.Started = DateTime.Now.Ticks;

			try
			{
				var prop = properties ?? _channel.RentBasicProperties();
				prop.CorrelationId = correlationId.ToString();
				prop.ReplyTo = _replyQueueName.Name;
				// TODO: confirm this doesnt cause more overhead to rabbitmq
				if (_timeoutInMs.HasValue)
				{
					prop.Expiration = _timeoutInMs.ToString();
				}

				_channel.BasicPublishFast(exchange, routing, true, properties, buffer);
			}
			catch (Exception ex)
			{
				// release spot
				Interlocked.Exchange(ref _pendingCalls[correlationId % _maxConcurrentCalls], null);

				_semaphoreSlim.Release();

				task.SetException(ex);
			}

			return task;
		}

		private bool SecureSpotAndUniqueCorrelationId(TaskSlim<MessageDelivery> task, out uint correlationId)
		{
			correlationId = 0;

			var tries = 0;
			while (tries++ < _maxConcurrentCalls)
			{
				var correlationIndex = _correlationCounter++;
				var pos = correlationIndex % _maxConcurrentCalls;

				if (Interlocked.CompareExchange(ref _pendingCalls[pos], task, null) == null)
				{
					correlationId = correlationIndex;
					return true;
				}
			}

			return false;
		}

		public void Dispose()
		{
			if (_disposed) return;

			_disposed = true;

			this._timeoutTimer.Dispose();

			if (!string.IsNullOrEmpty(_subscription))
			{
				_channel.BasicCancel(_subscription, false); 
			}
			
			DrainPendingCalls();
		}

		/*
		private TaskSlim<MessageDelivery> ReleaseSpot(string correlationId, out uint correlationIndex)
		{
			correlationIndex = UInt32.Parse(correlationId);
			var pos = correlationIndex % _maxConcurrentCalls;

			return Interlocked.Exchange(ref _pendingCalls[pos], null);
		}
		*/

		private void DrainPendingCalls()
		{
			foreach (var pendingCall in _pendingCalls)
			{
				if (pendingCall == null) continue;
				pendingCall.SetException(new Exception("Cancelled due to shutdown"), runContinuationAsync: true);
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