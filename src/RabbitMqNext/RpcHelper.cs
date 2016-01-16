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
		// private readonly int _timeoutInMs;

		private readonly ObjectPool<TaskSlim<MessageDelivery>> _taskResultPool;
		private readonly TaskSlim<MessageDelivery>[] _pendingCalls;
		private readonly int _timeoutInMs;
		private readonly long _timeoutInTicks;
		private readonly Timer _timeoutTimer;

		public RpcHelper(Channel channel, int maxConcurrentCalls, ConsumeMode mode, int timeoutInMs = 3000)
		{
			if (maxConcurrentCalls <= 0) throw new ArgumentOutOfRangeException("maxConcurrentCalls");

			_channel = channel;
			_maxConcurrentCalls = maxConcurrentCalls;
			_mode = mode;
			_timeoutInMs = timeoutInMs;
			_timeoutInTicks = timeoutInMs * TimeSpan.TicksPerMillisecond;

			// the impl keeps a timer pool so this is light and efficient
			_timeoutTimer = new System.Threading.Timer(OnTimeoutCheck, null, timeoutInMs, timeoutInMs);

			_pendingCalls = new TaskSlim<MessageDelivery>[maxConcurrentCalls];
			_taskResultPool = new ObjectPool<TaskSlim<MessageDelivery>>(() =>
				new TaskSlim<MessageDelivery>((inst) => _taskResultPool.PutObject(inst)), maxConcurrentCalls, true);
		}

		internal async Task Setup()
		{
			_replyQueueName = await _channel.QueueDeclare("", 
				false, false, exclusive: true, autoDelete: true, 
				waitConfirmation: true, arguments: null);

			_subscription = await _channel.BasicConsume(_mode, OnReplyReceived, _replyQueueName.Name, 
				consumerTag: "abc" + new Random().Next(100000), 
				withoutAcks: true, exclusive: true, arguments: null, waitConfirmation: true);
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
				taskLight.SetResult(delivery); // needs to be synchrounous
			}

			return Task.CompletedTask;
		}

		public TaskSlim<MessageDelivery> Call(string exchange, string routing, BasicProperties properties, ArraySegment<byte> buffer)
		{
			var task = _taskResultPool.GetObject();

			uint correlationId;
			if (!SecureSpotAndUniqueCorrelationId(task, out correlationId))
			{
				Console.WriteLine("max calls reached!");
				task.SetException(new Exception("reached max calls"));
				return task;
			}

			task.Id = correlationId; // so we can confirm we have the right instance later
			task.Started = DateTime.Now.Ticks;

			try
			{
				var prop = properties ?? new BasicProperties();
				prop.CorrelationId = correlationId.ToString();
				prop.ReplyTo = _replyQueueName.Name;
				// TODO: confirm this doesnt cause more overhead to rabbitmq
				prop.Expiration = _timeoutInMs.ToString();

				_channel.BasicPublishFast(exchange, routing, true, false, properties, buffer);
			}
			catch (Exception ex)
			{
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
					pendingCall.SetException(new Exception("Rpc call timeout"), runContinuationAsync: true);
				}
			}
		}
	}
}