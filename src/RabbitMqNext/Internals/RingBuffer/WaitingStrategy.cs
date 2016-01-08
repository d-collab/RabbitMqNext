namespace RabbitMqNext.Internals.RingBuffer
{
	using System;
	using System.Threading;
	using System.Threading.Tasks;
	using Locks;

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

		public abstract Task WaitAsync();
	}

	internal class LockWaitingStrategy : WaitingStrategy
	{
		private readonly ManualResetEventSlim _manualResetEvent = new ManualResetEventSlim(false, 50);
		private readonly AsyncAutoResetEvent _asyncAutoReset = new AsyncAutoResetEvent();
//		private volatile int _waiters;

		public LockWaitingStrategy(CancellationToken token) : base(token)
		{
		}

		public override Task WaitAsync()
		{
			return _asyncAutoReset.WaitAsync(this._token);
		}

		public override void Wait()
		{
			_manualResetEvent.Wait();
			_manualResetEvent.Reset();
		}

		public override void Signal()
		{
			_manualResetEvent.Set();
			_asyncAutoReset.Set();
		}

		public override void Dispose()
		{
			_manualResetEvent.Dispose();
		}
	}
}
