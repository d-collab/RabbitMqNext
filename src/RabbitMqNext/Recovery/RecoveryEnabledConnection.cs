namespace RabbitMqNext.Recovery
{
	using System;
	using System.Collections.Generic;
	using System.Linq;
	using System.Threading;
	using System.Threading.Tasks;
	using Internals;
	using Internals.RingBuffer;

	internal enum RecoveryAction
	{
		NoAction,
		WillReconnect
	}

	public class RecoveryEnabledConnection : IRecoveryEnabledConnection
	{
		const string LogSource = "RecoveryEnabledConnection";

		private readonly IEnumerable<string> _hostnames;
		private readonly AutoRecoverySettings _recoverySettings;
		private readonly List<RecoveryEnabledChannel> _channelRecoveries;
		private volatile bool _disableRecovery;
		private string _selectedHostname;
		private int _inRecovery;
		private bool _disposed;

		internal readonly Connection _connection;

		private readonly CancellationTokenSource _recoveryCancellationTokenSource;

		public event Action WillRecover;
		public event Action RecoveryCompleted;
		public event Action<Exception> RecoveryFailed;

		public event Action<string> ConnectionBlocked
		{
			add { _connection.ConnectionBlocked += value; }
			remove { _connection.ConnectionBlocked -= value; }
		}

		public event Action ConnectionUnblocked
		{
			add { _connection.ConnectionUnblocked += value; }
			remove { _connection.ConnectionUnblocked -= value; }
		}

		public RecoveryEnabledConnection(string hostname, Connection connection, AutoRecoverySettings recoverySettings)
			: this(new[] { hostname }, connection, recoverySettings)
		{
		}

		public RecoveryEnabledConnection(IEnumerable<string> hostnames, Connection connection, AutoRecoverySettings recoverySettings)
		{
			_hostnames = hostnames;
			_connection = connection;
			_recoverySettings = recoverySettings;
			_connection.Recovery = this;
			_channelRecoveries = new List<RecoveryEnabledChannel>();

			_recoveryCancellationTokenSource = new CancellationTokenSource();
		}

		internal RecoveryEnabledChannel CreateChannelRecovery(Channel channel)
		{
			var channelRecovery = new RecoveryEnabledChannel(channel, _recoverySettings);
			lock (_channelRecoveries)
			{
				_channelRecoveries.Add(channelRecovery);
			}
			return channelRecovery;
		}

		public void NotifyConnected(string hostname)
		{
			_selectedHostname = hostname;

			if (LogAdapter.IsDebugEnabled) LogAdapter.LogDebug(LogSource, "Connected to " + hostname);
		}

		internal RecoveryAction NotifyAbruptClose(Exception reason)
		{
			if (_disableRecovery)
			{
				if (LogAdapter.IsDebugEnabled) LogAdapter.LogDebug(LogSource, "NotifyAbrupt skipping action since disconect was initiated by the user", reason);
				return RecoveryAction.NoAction;
			}

			// TODO: Block RecoveryChannels? So new operations are blocked. also should fire notifications to indicate recovery in progress

			if (LogAdapter.IsDebugEnabled) LogAdapter.LogDebug(LogSource, "NotifyAbrupt", reason);

			TryInitiateRecovery();

			return RecoveryAction.WillReconnect;
		}

		internal RecoveryAction NotifyCloseByServer()
		{
			if (LogAdapter.IsDebugEnabled) LogAdapter.LogDebug(LogSource, "NotifyClosedByServer");

			// TODO: Block RecoveryChannels? So new operations are blocked. also should fire notifications to indicate recovery in progress

			TryInitiateRecovery();

			return RecoveryAction.WillReconnect;
		}

		internal void NotifyCloseByUser()
		{
			_disableRecovery = true;

			if (LogAdapter.IsDebugEnabled) LogAdapter.LogDebug(LogSource, "NotifyClosedByUser");
		}

		#region Implementation of IConnection

		public void AddErrorCallback(Func<AmqpError, Task> errorCallback)
		{
			_connection.AddErrorCallback(errorCallback);
		}

		public void RemoveErrorCallback(Func<AmqpError, Task> errorCallback)
		{
			_connection.RemoveErrorCallback(errorCallback);
		}

		public bool IsClosed
		{
			get { return _connection.IsClosed; }
		}
		
		public async Task<IChannel> CreateChannel(ChannelOptions options)
		{
			var channel = (Channel) await _connection.CreateChannel(options).ConfigureAwait(false);
			return CreateChannelRecovery(channel);
		}

		public async Task<IChannel> CreateChannelWithPublishConfirmation(ChannelOptions options, int maxunconfirmedMessages = 100)
		{
			var channel = (Channel) await _connection.CreateChannelWithPublishConfirmation(options, maxunconfirmedMessages).ConfigureAwait(false);
			return CreateChannelRecovery(channel);
		}

		public void Dispose()
		{
			if (_disposed) return;
			_disposed = true;

			if (LogAdapter.ExtendedLogEnabled)
				LogAdapter.LogDebug(LogSource, "Dispose called");

			_recoveryCancellationTokenSource.Cancel();

			_disableRecovery = true;
			
			_connection.Dispose();

			// not disposing _recoveryCancellationTokenSource. for now leaving for finalizer
		}

		#endregion

		private void TryInitiateRecovery()
		{
			if (Interlocked.CompareExchange(ref _inRecovery, 1, 0) == 0)
			{
				if (LogAdapter.IsWarningEnabled) LogAdapter.LogWarn(LogSource, "TryInitiateRecovery starting recovery process");

				// block all channels

				lock (_channelRecoveries)
				foreach (var recoveryEnabledChannel in _channelRecoveries)
				{
					recoveryEnabledChannel.Disconnected();
				}

				// from this point on, no api calls are allowed on the channel decorators

				ThreadFactory.BackgroundThread(async (pthis) =>
				{
					try
					{
						if (LogAdapter.IsWarningEnabled) LogAdapter.LogWarn(LogSource, "Starting Recovery. Waiting for connection clean up");

						await pthis.AwaitConnectionReset();

						if (LogAdapter.IsWarningEnabled) LogAdapter.LogWarn(LogSource, "Connection is ready");

						pthis.FireWillRecover();

						pthis.ResetConnection();

						var didConnect = await pthis.CycleReconnect().ConfigureAwait(false);
						if (!didConnect)
						{
							// Cancelled
							pthis.FireRecoveryFailed(new Exception("Could not connect to any host"));
							return;
						}

						if (LogAdapter.IsWarningEnabled) LogAdapter.LogWarn(LogSource, "Reconnected");

						await pthis.Recover().ConfigureAwait(false);

						pthis.FireRecoveryCompleted();

						if (LogAdapter.IsWarningEnabled) LogAdapter.LogWarn(LogSource, "Completed");
					}
					catch (Exception ex)
					{
						if (LogAdapter.IsWarningEnabled) LogAdapter.LogWarn(LogSource, "TryInitiateRecovery error", ex);
						
						pthis.HandleRecoveryFatalError(ex);
						
						pthis.FireRecoveryFailed(ex);
					}
					finally
					{
						Interlocked.Exchange(ref pthis._inRecovery, 0);
					}

				}, "RecoveryProc", this);
			}
			else
			{
				if (LogAdapter.IsDebugEnabled) LogAdapter.LogDebug(LogSource, "TryInitiateRecovery: recovery in progress. skipping");
			}
		}

		private void ResetConnection()
		{
			_connection.Reset();
		}

		// Runs from a background thread
		private async Task<bool> CycleReconnect()
		{
			var hosts = _hostnames.ToArray();
			var index = 0;
			var firstRun = true;

			// TODO: maxattempts or api hook to allow continuing/stopping
			while (!_recoveryCancellationTokenSource.Token.IsCancellationRequested)
			{
				var hostToTry = hosts[index++ % hosts.Length];

				if (hostToTry == _selectedHostname && firstRun) // skip the same hostname, but only once
				{
					firstRun = false;
					continue;
				}

				if (LogAdapter.IsWarningEnabled) LogAdapter.LogWarn(LogSource, "Trying to re-connect to " + hostToTry);

				var succeeded = await _connection.InternalConnect(hostToTry, throwOnError: false).ConfigureAwait(false);
				if (succeeded) return true;

				// TODO: config/parameter for wait time
				Thread.Sleep(1000);
			}

			return false;
		}

		private async Task Recover()
		{
			RecoveryEnabledChannel[] channelsToRecover;

			lock (_channelRecoveries)
				channelsToRecover = _channelRecoveries.ToArray();

			foreach (var recoveryEnabledChannel in channelsToRecover)
			{
				if (LogAdapter.ExtendedLogEnabled)
					LogAdapter.LogWarn(LogSource, "Recover: recovering channel " + recoveryEnabledChannel.ChannelNumber);

				try
				{
					await recoveryEnabledChannel.DoRecover(_connection).ConfigureAwait(false);
				}
				catch (Exception ex)
				{
					if (LogAdapter.IsErrorEnabled) LogAdapter.LogError(LogSource, "Recover: error recovering channel " + recoveryEnabledChannel.ChannelNumber, ex);
					throw;
				}
			}
		}

		// When the recovery process completely failed, close everything
		private void HandleRecoveryFatalError(Exception ex)
		{
			// TODO: implement this
		}

		private void FireRecoveryCompleted()
		{
			var ev = this.RecoveryCompleted;
			if (ev != null) ev();
		}

		/// <summary>
		/// Returning Tasks is completed with the connection is wiped clean
		/// </summary>
		private Task AwaitConnectionReset()
		{
			var tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

			_connection._io.TriggerOnceOnFreshState(() =>
			{
				tcs.SetResult(true);
			});

			return tcs.Task;
		}

		private void FireWillRecover()
		{
			var ev = this.WillRecover;
			if (ev != null) ev();
		}

		private void FireRecoveryFailed(Exception ex)
		{
			var ev = this.RecoveryFailed;
			if (ev != null) ev(ex);
		}
	}
}
