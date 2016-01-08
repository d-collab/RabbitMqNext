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

		public abstract void WaitForRead();
		
		public abstract void WaitForWrite();

		public abstract void SignalReadDone();

		public abstract void SignalWriteDone();

		public abstract void Dispose();

		public virtual Task WaitForReadAsync()
		{
			WaitForRead();
			return Task.CompletedTask;
		}

		public virtual Task WaitForWriteAsync()
		{
			WaitForWrite();
			return Task.CompletedTask;
		}
	}

	internal class LockWaitingStrategy : WaitingStrategy
	{
		private readonly AutoResetEvent _read = new AutoResetEvent(false);
		private readonly AutoResetEvent _write = new AutoResetEvent(false);

		// private readonly ManualResetEventSlim _manualResetEvent = new ManualResetEventSlim(false, 50);
//		private readonly ManualResetEventSlim _manualResetEvent = new ManualResetEventSlim(false, 50);
//		private readonly AsyncAutoResetEvent _asyncAutoReset = new AsyncAutoResetEvent();
//		private volatile int _waiters;

		public LockWaitingStrategy(CancellationToken token) : base(token)
		{
		}

		public override void WaitForRead()
		{
			_read.WaitOne();
		}

		public override void WaitForWrite()
		{
			_write.WaitOne();
		}

		public override void SignalReadDone()
		{
			_read.Set();
		}

		public override void SignalWriteDone()
		{
			_write.Set();
		}

		public override void Dispose()
		{
			_read.Dispose();
			_write.Dispose();
		}
	}
}
