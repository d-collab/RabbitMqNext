namespace RabbitMqNext
{
	using System;
	using System.Runtime.CompilerServices;
	using System.Threading;
	using System.Threading.Tasks;
	using RabbitMqNext.Io;
	using RabbitMqNext.Internals;

	public class Connection : IDisposable
	{
		private readonly ConnectionIO _io;

		internal const int MaxChannels = 10;
		private readonly Channel[] _channels = new Channel[MaxChannels + 1]; // 1-based index
		private int _channelNumbers;

		public Connection()
		{
			_io = new ConnectionIO(this)
			{
				OnError = this.OnError
			};
		}

		public ConnectionRecoveryStrategy ConnectionRecoveryStrategy { get; internal set; }

		public event Action<AmqpError> OnError;

		internal async Task<bool> Connect(string hostname, string vhost, 
										  string username, string password, 
										  int port, bool throwOnError = true)
		{
			var result = await _io.InternalDoConnectSocket(hostname, port, throwOnError).ConfigureAwait(false);
			
			if (!result) return false;
			
			return await _io.Handshake(vhost, username, password, throwOnError).ConfigureAwait(false);
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
	}
}
