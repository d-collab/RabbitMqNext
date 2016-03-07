namespace RabbitMqNext
{
	using System;
	using System.Runtime.CompilerServices;
	using System.Threading;
	using System.Threading.Tasks;
	using RabbitMqNext.Io;
	using RabbitMqNext.Internals;


	public sealed class Connection : IConnection
	{
		internal readonly ConnectionIO _io;

		internal const int MaxChannels = 10;
		private readonly Channel[] _channels = new Channel[MaxChannels + 1]; // 1-based index
		private int _channelNumbers;
		private ConnectionInfo _connectionInfo;

		public Connection()
		{
			_io = new ConnectionIO(this)
			{
				OnError = this.OnError
			};
		}

		public AutoRecoveryEnabledConnection Recovery { get; internal set; }

		public event Action<AmqpError> OnError;

		internal Task<bool> Connect(string hostname, string vhost, 
									string username, string password, 
									int port, bool throwOnError = true)
		{
			// Saves info for reconnection scenarios

			_connectionInfo = new ConnectionInfo 
			{ 
				hostname = hostname, 
				vhost = vhost, 
				username = username, 
				password = password, 
				port = port 
			};

			return InternalConnect(hostname);
		}

		internal async Task<bool> InternalConnect(string hostname, bool throwOnError = true)
		{
			var result = await _io.InternalDoConnectSocket(hostname, _connectionInfo.port, throwOnError).ConfigureAwait(false);

			if (!result) return false;

			result = await _io.Handshake(_connectionInfo.vhost, 
				_connectionInfo.username, _connectionInfo.password, throwOnError).ConfigureAwait(false);

			if (result && this.Recovery != null)
			{
				this.Recovery.NotifyConnected(hostname);
			}

			return result;
		}

		public bool IsClosed { get { return _io.IsClosed; } }

		public Task<IChannel> CreateChannel()
		{
			return InternalCreateChannel(withPubConfirm: false);
		}

		public Task<IChannel> CreateChannelWithPublishConfirmation(int maxunconfirmedMessages = 100)
		{
			return InternalCreateChannel(maxunconfirmedMessages, withPubConfirm: true);
		}

		public void Dispose()
		{
			if (this.Recovery != null)
				this.Recovery.Dispose();

			this._io.Dispose();
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		internal ChannelIO ResolveChannel(ushort channel)
		{
			if (channel > MaxChannels)
			{
				LogAdapter.LogError("Connection", "ResolveChannel for invalid channel " + channel);
				throw new Exception("Unexpected channel number " + channel);
			}

			var channelInst = _channels[channel];
			if (channelInst == null)
			{
				LogAdapter.LogError("Connection", "ResolveChannel for non-initialized channel " + channel);
				throw new Exception("Channel not initialized " + channel);
			}

			return channelInst._io;
		}

		internal void CloseAllChannels(Exception reason)
		{
			foreach (var channel in _channels)
			{
				if (channel == null) continue;

				channel._io.InitiateAbruptClose(reason);
			}
		}

		internal void CloseAllChannels(bool initiatedByServer, AmqpError error)
		{
			foreach (var channel in _channels)
			{
				if (channel == null) continue;

#pragma warning disable 4014
				channel._io.InitiateCleanClose(initiatedByServer, error);
#pragma warning restore 4014
			}
		}

		private async Task<IChannel> InternalCreateChannel(int maxunconfirmedMessages = 0, bool withPubConfirm = false)
		{
			var channelNum = (ushort)Interlocked.Increment(ref _channelNumbers);

			if (channelNum > MaxChannels)
				throw new Exception("Exceeded channel limits");

			var channel = new Channel(channelNum, this._io, this._io._cancellationToken);

			try
			{
				_channels[channelNum] = channel;
				await channel.Open().ConfigureAwait(false);
				if (withPubConfirm)
				{
					await channel.EnableConfirmation(maxunconfirmedMessages).ConfigureAwait(false);
				}
				return channel;
			}
			catch
			{
				// TODO: release channel number that wasnt used
				_channels[channelNum] = null;
				throw;
			}
		}


		internal void NotifyAbruptClose(Exception reason)
		{
			if (this.Recovery != null)
				this.Recovery.NotifyAbruptClose(reason);

			// this.CloseAllChannels(reason);
		}

		internal void NotifyCloseByUser()
		{
			if (this.Recovery != null)
				this.Recovery.NotifyCloseByUser();
		}

		internal void NotifyCloseByServer()
		{
			if (this.Recovery != null)
				this.Recovery.NotifyCloseByServer();
		}

		internal class ConnectionInfo
		{
			public string hostname;
			public string vhost;
			public string username;
			public string password;
			public int port;
		}
	}
}
