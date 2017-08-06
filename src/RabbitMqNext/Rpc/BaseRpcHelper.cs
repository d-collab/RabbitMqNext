namespace RabbitMqNext
{
	using System;
	using System.Collections.Generic;
	using System.ServiceModel.PeerResolvers;
	using System.Threading;
	using System.Threading.Tasks;


	public abstract class BaseRpcHelper 
	{
		protected static readonly Random Rnd = new Random();

		protected IChannel _channel;
		protected readonly ConsumeMode _mode;
		protected readonly SemaphoreSlim _semaphoreSlim;
		protected readonly int _maxConcurrentCalls;

		protected AmqpQueueInfo _replyQueueName;
//		protected string _subscription;

		protected bool _operational;

		/// <summary>
		/// If we want the awaits to capture the context (required for WPF/Web environments)
		/// </summary>
		public bool CaptureContext;

		protected BaseRpcHelper(ConsumeMode mode, IChannel channel, int maxConcurrentCalls)
		{
			if (maxConcurrentCalls <= 0) throw new ArgumentOutOfRangeException("maxConcurrentCalls");

			_mode = mode;
			_channel = channel;
			_maxConcurrentCalls = maxConcurrentCalls;
			_semaphoreSlim = new SemaphoreSlim(maxConcurrentCalls, maxConcurrentCalls);
		}

		public IChannel Channel { get { return _channel; } }

		// recovery support

		/// <summary>
		/// Signal that pending calls should be cancelled
		/// </summary>
		public void SignalInRecovery()
		{
			this.DrainPendingCalls();

			_operational = false;
		}

		/// <summary>
		/// Called once a new healthy channel has been created.
		/// </summary>
		public Task SignalRecovered(IDictionary<string, string> reservedNamesMapping)
		{
			// return Setup();
			UpdateInfo(reservedNamesMapping);

			return Task.CompletedTask;
		}

		// end recovery support

		protected abstract void DrainPendingCalls();

		protected async Task Setup()
		{
			_replyQueueName = await _channel.QueueDeclare("", // temp
				false, false, exclusive: true, autoDelete: true,
				waitConfirmation: true, arguments: null).ConfigureAwait(this.CaptureContext);

			/*_subscription =*/ await _channel.BasicConsume(_mode, OnReplyReceived, _replyQueueName.Name,
				consumerTag: "",
				withoutAcks: true, exclusive: true, arguments: null, waitConfirmation: true).ConfigureAwait(this.CaptureContext);

			_operational = true; 
		}

		protected void UpdateInfo(IDictionary<string, string> reservedNamesMapping)
		{
			_replyQueueName = new AmqpQueueInfo { Name = reservedNamesMapping[_replyQueueName.Name]  };

			_operational = true;
		}

		protected abstract Task OnReplyReceived(MessageDelivery delivery);
	}
}