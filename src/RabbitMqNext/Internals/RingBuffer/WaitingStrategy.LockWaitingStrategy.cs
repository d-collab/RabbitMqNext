namespace RabbitMqNext.Internals.RingBuffer
{
	using System.Threading;
	using System.Threading.Tasks;
	using Locks;

	/// <summary>
	/// needs a better impl
	/// </summary>
	internal class LockWaitingStrategy : WaitingStrategy
	{
//		private readonly AutoResetEvent _read = new AutoResetEvent(false);
//		private readonly AutoResetEvent _write = new AutoResetEvent(false);
		private readonly AutoResetSuperSlimLock _read = new AutoResetSuperSlimLock();
		private readonly AutoResetSuperSlimLock _write = new AutoResetSuperSlimLock();

		public LockWaitingStrategy(CancellationToken token) : base(token)
		{
		}

		public override void WaitForRead()
		{
			_read.Wait();
		}

		public override void WaitForWrite()
		{
			_write.Wait();
		}

		public override void SignalReadDone()
		{
			_read.Set();
		}

		public override void SignalWriteDone()
		{
			_write.Set();
		}

		public override Task WaitForReadAsync()
		{
			return _read.WaitAsync();
		}

		public override Task WaitForWriteAsync()
		{
			return _write.WaitAsync();
		}

		public override void Dispose()
		{
			_read.Dispose();
			_write.Dispose();
		}
	}
}