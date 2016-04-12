namespace RabbitMqNext
{
	using System;
	using System.Threading;
	using System.Threading.Tasks;


	public abstract class BaseRpcHelper 
	{
		protected static readonly Random Rnd = new Random();

		protected Channel _channel;
		protected readonly ConsumeMode _mode;
		protected readonly SemaphoreSlim _semaphoreSlim;
		protected readonly int _maxConcurrentCalls;

		protected AmqpQueueInfo _replyQueueName;
		protected string _subscription;


		protected BaseRpcHelper(ConsumeMode mode, Channel channel, int maxConcurrentCalls)
		{
			if (maxConcurrentCalls <= 0) throw new ArgumentOutOfRangeException("maxConcurrentCalls");

			_mode = mode;
			_channel = channel;
			_maxConcurrentCalls = maxConcurrentCalls;
			_semaphoreSlim = new SemaphoreSlim(maxConcurrentCalls, maxConcurrentCalls);
		}

		// recovery support

		/// <summary>
		/// Signal that pending calls should be cancelled
		/// </summary>
		public void SignalInRecovery()
		{
			this.DrainPendingCalls();
		}

		/// <summary>
		/// Called once a new healthy channel has been created.
		/// </summary>
		public Task SignalRecovered(Channel newChannel)
		{
			this._channel = newChannel;

			return Setup();
		}

		// end recovery support

		protected abstract void DrainPendingCalls();

		protected async Task Setup()
		{
			Console.WriteLine("Setup Rpc");

			_replyQueueName = await _channel.QueueDeclare("", // temp
				false, false, exclusive: true, autoDelete: true,
				waitConfirmation: true, arguments: null).ConfigureAwait(false);

			_subscription = await _channel.BasicConsume(_mode, OnReplyReceived, _replyQueueName.Name,
				consumerTag: "",
				withoutAcks: true, exclusive: true, arguments: null, waitConfirmation: true).ConfigureAwait(false);
		}

		protected abstract Task OnReplyReceived(MessageDelivery delivery);
	}
}