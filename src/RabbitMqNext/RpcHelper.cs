namespace RabbitMqNext
{
	using System;
	using System.Threading;
	using System.Threading.Tasks;
	using Internals;

	public class RpcHelper : IDisposable
	{
		private readonly AmqpChannel _channel;
		private readonly int _maxConcurrentCalls;
		private readonly ConsumeMode _mode;
		private volatile uint _correlationCounter;
		private volatile bool _disposed;
		private AmqpQueueInfo _replyQueueName;
		private string _subscription;
		// private readonly int _timeoutInMs;

		private readonly ObjectPool<TaskLight<MessageDelivery>> _taskResultPool;
		private readonly TaskLight<MessageDelivery>[] _pendingCalls;

		public RpcHelper(AmqpChannel channel, int maxConcurrentCalls, ConsumeMode mode, int timeoutInMs = 3000)
		{
			if (maxConcurrentCalls <= 0) throw new ArgumentOutOfRangeException("maxConcurrentCalls");

			_channel = channel;
			_maxConcurrentCalls = maxConcurrentCalls;
			_mode = mode;
//			_timeoutInMs = timeoutInMs;
			_pendingCalls = new TaskLight<MessageDelivery>[maxConcurrentCalls];
			_taskResultPool = new ObjectPool<TaskLight<MessageDelivery>>(() =>
				new TaskLight<MessageDelivery>((inst) => _taskResultPool.PutObject(inst)), maxConcurrentCalls, true);
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

			var task = Interlocked.Exchange(ref _pendingCalls[pos], null);

			if (task == null)
			{
				// the helper was disposed and the task list was drained.
				if (_disposed) return Task.CompletedTask;
				throw new Exception("Something is seriously wrong");
			}

			task.SetResult(delivery); // needs to be synchrounous

			return Task.CompletedTask;
		}

		public TaskLight<MessageDelivery> Call(string exchange, string routing, BasicProperties properties, ArraySegment<byte> buffer)
		{
			var task = _taskResultPool.GetObject();
			// var task = new TaskCompletionSource<MessageDelivery>(null);

			uint correlationId;
			if (!SecureSpotAndUniqueCorrelationId(task, out correlationId))
			{
				Console.WriteLine("max calls reached!");
				task.SetException(new Exception("reached max calls"));
				return task;
			}

			try
			{
				var prop = properties ?? new BasicProperties();
				prop.CorrelationId = correlationId.ToString();
				prop.ReplyTo = _replyQueueName.Name;

				_channel.BasicPublishN(exchange, routing, true, false, properties, buffer);
			}
			catch (Exception ex)
			{
				task.SetException(ex);
			}

			return task;
		}

		private bool SecureSpotAndUniqueCorrelationId(TaskLight<MessageDelivery> task, out uint correlationId)
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

			if (!string.IsNullOrEmpty(_subscription))
			{
				_channel.BasicCancel(_subscription, false); 
			}
			
			DrainPendingCalls();
		}

		private void DrainPendingCalls()
		{

		}
	}
}