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
}
