namespace RabbitMqNext
{
	using System;
	using System.Runtime.CompilerServices;
	using System.Threading;
	using System.Threading.Tasks;
	using Internals;


	public abstract class BaseRpcHelper<T> : BaseRpcHelper, IDisposable
	{
		private const string LogSource = "BaseRpcHelper";
		protected static readonly char[] Separator = { '_' };
		protected static readonly string SeparatorStr = Separator[0].ToString();

		protected readonly Timer _timeoutTimer;
		protected readonly int? _timeoutInMs;
		protected readonly long? _timeoutInTicks;
		protected readonly PendingCallState[] _pendingCalls;

		protected volatile uint _correlationCounter;
		protected volatile bool _disposed;

		protected class PendingCallState
		{
			public int cookie;
			public TaskCompletionSource<T> tcs;
			public long started;
		}

		protected BaseRpcHelper(Channel channel, int maxConcurrentCalls, ConsumeMode mode, int? timeoutInMs)
			: base(mode, channel, maxConcurrentCalls)
		{
			_timeoutInMs = timeoutInMs;

			// the impl keeps a timer pool so this is light and efficient
			if (timeoutInMs.HasValue)
			{
				_timeoutInTicks = timeoutInMs * TimeSpan.TicksPerMillisecond;
				_timeoutTimer = new System.Threading.Timer(OnTimeoutCheck, null, timeoutInMs.Value, timeoutInMs.Value);
			}

			_pendingCalls = new PendingCallState[maxConcurrentCalls];
			for (int i = 0; i < maxConcurrentCalls; i++)
			{
				_pendingCalls[i] = new PendingCallState();
			}
		}

		public void Dispose()
		{
			if (_disposed) return;
			Thread.MemoryBarrier();
			_disposed = true;

			if (_timeoutTimer != null)
			{
				this._timeoutTimer.Dispose();
			}

			if (!string.IsNullOrEmpty(_subscription))
			{
				try
				{
					_channel.BasicCancel(_subscription, false).Wait();
				}
				catch (Exception)
				{
					// no problem!
				}
			}

			DrainPendingCalls();
		}

		protected TaskCompletionSource<T> SecureSpotAndUniqueCorrelationId(out long pos, out uint correlationId)
		{
			var tcs = new TaskCompletionSource<T>(TaskCreationOptions.RunContinuationsAsynchronously);
			if (tcs.Task.Id == 0) // wrap protection
				tcs = new TaskCompletionSource<T>(TaskCreationOptions.RunContinuationsAsynchronously);

			correlationId = 0;
			pos = 0L;

			var tries = 0;
			while (tries++ < _maxConcurrentCalls)
			{
				var correlationIndex = _correlationCounter++;
				pos = correlationIndex % _maxConcurrentCalls;

				if (Interlocked.CompareExchange(ref _pendingCalls[pos].cookie, tcs.Task.Id, 0) == 0)
				{
					_pendingCalls[pos].started = DateTime.Now.Ticks;
					_pendingCalls[pos].tcs = tcs;

					correlationId = correlationIndex;

					// LogAdapter.LogDebug(LogSource, "Secured cookie " + tcs.Task.Id + " correlationIndex " + correlationIndex);

					return tcs;
				}

//				if (Interlocked.CompareExchange(ref _pendingCalls[pos], task, null) == null)
//				{
//					correlationId = correlationIndex;
//					return true;
//				}
			}

			return null;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		protected void GetPosAndCookieFromCorrelationId(string correlationId,
			out uint correlationIdVal, out long pos, out int cookie)
		{
			// zero alloc conversion

			var pastSeparator = false;
			correlationIdVal = 0;
			cookie = 0;
			const char Zero = '0';

			for (int i = 0; i < correlationId.Length; i++)
			{
				if (correlationId[i] == '_')
				{
					pastSeparator = true;
					continue;
				}
				if (!pastSeparator)
				{
					if (correlationIdVal != 0) correlationIdVal *= 10;
					correlationIdVal += (uint) (correlationId[i] - Zero);
				}
				else
				{
					if (cookie != 0) cookie *= 10;
					cookie += correlationId[i] - Zero;
				}
			}

			pos = correlationIdVal % _maxConcurrentCalls;

			LogAdapter.LogDebug(LogSource, "Received cookie " + cookie + " correlationIndex " + correlationIdVal);
		}

		protected override void DrainPendingCalls()
		{
			var exception = new Exception("Cancelled due to shutdown");

			for (int i = 0; i < _pendingCalls.Length; i++)
			{
				var pendingCall = _pendingCalls[i];

				if (pendingCall.tcs == null) continue;

				if (pendingCall.tcs.TrySetException(exception))
				{
					Interlocked.Exchange(ref pendingCall.cookie, 0);
					_semaphoreSlim.Release();
				}
			}
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		protected string BuildFullCorrelation(int cookie, uint correlationId)
		{
			return correlationId + SeparatorStr + cookie;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		protected void ReleaseSpot(long pos, int cookie)
		{
			if (cookie == 0) return; // no such thing as cookie = 0

			// setting it back to 0 frees the spot
			Interlocked.CompareExchange(ref _pendingCalls[pos].cookie, 0, cookie);
		}

		private void OnTimeoutCheck(object state)
		{
			var now = DateTime.Now.Ticks;

			foreach (var pendingCall in _pendingCalls)
			{
				if (pendingCall.cookie == 0) continue;

				var tcs = pendingCall.tcs;

				if (tcs != null && now - pendingCall.started > _timeoutInTicks)
				{
					LogAdapter.LogDebug(LogSource, "Timeout'ing item " + pendingCall.cookie);

					if (tcs.TrySetException(new Exception("Rpc call timeout")))
					{
						pendingCall.cookie = 0;
					}
				}
			}
		}
	}
}