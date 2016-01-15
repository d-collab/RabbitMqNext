namespace RabbitMqNext
{
	using System;
	using System.Runtime.CompilerServices;
	using System.Threading;
	using System.Threading.Tasks;
	using Internals;

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

		public event Action<AmqpError> OnError;

		internal async Task Connect(string hostname, string vhost, string username, string password, int port = 5672)
		{
			await _io.InternalDoConnectSocket(hostname, port);
			await _io.Handshake(vhost, username, password);
		}

		public bool IsClosed { get { return _io.IsClosed; } }

		public Task<Channel> CreateChannel()
		{
			return InternalCreateChannel(withPubConfirm: false);
		}

		public Task<Channel> CreateChannelWithPublishConfirmation(int maxunconfirmedMessages = 100)
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
				// TODO: Log
				Console.WriteLine("wtf??? " + channel);
				throw new Exception("Unexpecte channel number " + channel);
			}

			var channelInst = _channels[channel];
			if (channelInst == null)
			{
				// TODO: Log
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

		private async Task<Channel> InternalCreateChannel(int maxunconfirmedMessages = 0, bool withPubConfirm = false)
		{
			var channelNum = (ushort)Interlocked.Increment(ref _channelNumbers);

			if (channelNum > MaxChannels)
				throw new Exception("Exceeded channel limits");

			var channel = new Channel(channelNum, this._io, this._io._cancellationToken);

			try
			{
				_channels[channelNum] = channel;
				await channel.Open();
				if (withPubConfirm)
				{
					await channel.EnableConfirmation(maxunconfirmedMessages);
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
