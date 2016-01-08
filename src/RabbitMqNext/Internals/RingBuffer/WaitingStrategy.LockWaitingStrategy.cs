namespace RabbitMqNext.Internals.RingBuffer
{
	using System.Threading;

	/// <summary>
	/// needs a better impl
	/// </summary>
	internal class LockWaitingStrategy : WaitingStrategy
	{
		private readonly AutoResetEvent _read = new AutoResetEvent(false);
		private readonly AutoResetEvent _write = new AutoResetEvent(false);

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