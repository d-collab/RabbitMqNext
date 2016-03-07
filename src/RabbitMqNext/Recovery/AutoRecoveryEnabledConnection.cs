namespace RabbitMqNext
{
	using System;
	using System.Collections.Generic;
	using System.Linq;
	using System.Threading;
	using System.Threading.Tasks;
	using Internals;
	using Internals.RingBuffer;

	internal enum ConnectionRecoveryAction
	{
		NoAction,
		WillReconnect
	}

	public class RecoveryEnabledConnection : IConnection
	{
		const string LogSource = "ConnectionRecovery";

		private readonly IEnumerable<string> _hostnames;
		private readonly Connection _connection;
		private readonly List<RecoveryEnabledChannel> _channelRecoveries;
		private volatile bool _disableRecovery;
		private string _selectedHostname;

		public RecoveryEnabledConnection(string hostname, Connection connection) 
			: this(new [] { hostname }, connection)
		{
		}

		public RecoveryEnabledConnection(IEnumerable<string> hostnames, Connection connection)
		{
			_hostnames = hostnames;
			_connection = connection;
			_connection.Recovery = this;
			_channelRecoveries = new List<RecoveryEnabledChannel>();
		}

		internal RecoveryEnabledChannel CreateChannelRecovery(Channel channel)
		{
			var channelRecovery = new RecoveryEnabledChannel(channel);
			lock (_channelRecoveries)
				_channelRecoveries.Add(channelRecovery);
			return channelRecovery;
		}

		public void NotifyConnected(string hostname)
		{
			_selectedHostname = hostname;

			LogAdapter.LogDebug(LogSource, "Connected to " + hostname);
		}

		private int _inRecovery;

		internal ConnectionRecoveryAction NotifyAbruptClose(Exception reason)
		{
			if (_disableRecovery)
			{
				LogAdapter.LogDebug(LogSource, "NotifyAbrupt skipping action since disconect was initiated by the user", reason);
				return ConnectionRecoveryAction.NoAction;
			}

			LogAdapter.LogDebug(LogSource, "NotifyAbrupt", reason);

			TryInitiateRecovery();

			return ConnectionRecoveryAction.WillReconnect;
		}

		internal void NotifyCloseByUser()
		{
			_disableRecovery = true;

			LogAdapter.LogDebug(LogSource, "NotifyClosedByUser ");
		}

		internal ConnectionRecoveryAction NotifyCloseByServer()
		{
			LogAdapter.LogDebug(LogSource, "NotifyClosedByServer ");

			TryInitiateRecovery();

			return ConnectionRecoveryAction.WillReconnect;
		}

		#region Implementation of IConnection

		public event Action<AmqpError> OnError
		{
			add { _connection.OnError += value; }
			remove { _connection.OnError -= value; }
		}

		public bool IsClosed
		{
			get { return _connection.IsClosed; }
		}
		
		public async Task<IChannel> CreateChannel()
		{
			return CreateChannelRecovery(await _connection.CreateChannel() as Channel);
		}

		public async Task<IChannel> CreateChannelWithPublishConfirmation(int maxunconfirmedMessages = 100)
		{
			return CreateChannelRecovery(await _connection.CreateChannelWithPublishConfirmation(maxunconfirmedMessages) as Channel);
		}

		public void Dispose()
		{
			if (LogAdapter.ExtendedLogEnabled)
				LogAdapter.LogDebug(LogSource, "Dispose called");

			_disableRecovery = true;
			
			_connection.Dispose();
		}

		#endregion

		private void TryInitiateRecovery()
		{
			if (Interlocked.CompareExchange(ref _inRecovery, 1, 0) == 0)
			{
				LogAdapter.LogDebug(LogSource, "TryInitiateRecovery starting recovery process");

				ThreadFactory.BackgroundThread(async (pthis) =>
				{
					try
					{
						await pthis.CycleReconnect();
						await pthis.Recover();
					}
					catch (Exception ex)
					{
						LogAdapter.LogError(LogSource, "TryInitiateRecovery error", ex);
						pthis.HandleRecoveryFatalError();
					}
					finally
					{
						Interlocked.Exchange(ref pthis._inRecovery, 0);
					}

				}, "RecoveryProc", this);
			}
			else
			{
				LogAdapter.LogDebug(LogSource, "TryInitiateRecovery: recovery in progress. skipping");
			}
		}

		// Runs from a background thread
		private async Task<bool> CycleReconnect()
		{
			var hosts = _hostnames.ToArray();
			var index = 0;
			var firstRun = true;

			// TODO: maxattempts or api hook to allow continuing/stopping
			while (true)
			{
				var hostToTry = hosts[index++ % hosts.Length];

				if (hostToTry == _selectedHostname && firstRun) // skip the same hostname, but only once
				{
					firstRun = false;
					continue;
				}

				var succeeded = await _connection.InternalConnect(hostToTry, throwOnError: false);
				if (succeeded)
					return true;

				// TODO: config/parameter for wait time
				Thread.Sleep(1000);
			}
		}

		private Task Recover()
		{
			// Recover channels
			// Recover declares
			// Recover bindings
			// Recover subscriptions
			// Recover rpchelpers

			return Task.CompletedTask;
		}

		// When the recovery process completely failed, close everything
		private void HandleRecoveryFatalError()
		{
			// TODO: implement this
		}
	}
}
