namespace RabbitMqNext.Internals.RingBuffer
{
	using System;
	using System.Threading;

	internal abstract class WaitingStrategy : IDisposable
	{
		protected readonly CancellationToken _token;

		protected WaitingStrategy(CancellationToken token)
		{
			_token = token;
		}

		public abstract void Wait();

		public abstract void Signal();

		public abstract void Dispose();
	}

	internal class LockWaitingStrategy : WaitingStrategy
	{
		private readonly ManualResetEventSlim _manualResetEvent = new ManualResetEventSlim(false, 50);
//		private volatile int _waiters;

		public LockWaitingStrategy(CancellationToken token) : base(token)
		{
		}

		public override void Wait()
		{
			_manualResetEvent.Wait();
			_manualResetEvent.Reset();
		}

		public override void Signal()
		{
			_manualResetEvent.Set();
		}

		public override void Dispose()
		{
			_manualResetEvent.Dispose();
		}
	}
}
