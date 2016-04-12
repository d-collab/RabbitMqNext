namespace RabbitMqNext
{
	using System;
	using System.Threading;

	public abstract class BaseRpcHelper 
	{
		protected static readonly Random Rnd = new Random();

		protected Channel _channel;
		protected readonly SemaphoreSlim _semaphoreSlim;
		protected readonly int _maxConcurrentCalls;

		protected BaseRpcHelper(int maxConcurrentCalls)
		{
			if (maxConcurrentCalls <= 0) throw new ArgumentOutOfRangeException("maxConcurrentCalls");

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
		public void SignalRecovered(Channel newChannel)
		{
			this._channel = newChannel;
		}

		// end recovery support

		protected abstract void DrainPendingCalls();
	}
}