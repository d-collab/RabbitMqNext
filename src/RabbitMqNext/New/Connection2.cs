namespace RabbitMqNext
{
	using System;
	using System.Collections.Generic;
	using System.Net;
	using System.Net.Sockets;
	using System.Runtime.CompilerServices;
	using System.Threading;
	using System.Threading.Tasks;
	using Internals;

	// connection concerns:
	// - handshake, auth, connection open/tune/etc
	// operations: 
	// * Create channel
	// * keeps an eye on flow, heartbeat, etc
	// 
	// keeps socket
	// close: closes all channels (drain), connection (drain pending tasks)

	public class Connection2 : IDisposable
	{
		private readonly CancellationTokenSource _cancellationTokenSrc;
		
		#region populated by server replies
		internal IDictionary<string, object> _serverProperties;
		internal string _mechanisms;
		internal ushort _heartbeat;
		internal string _knownHosts;
		internal ushort _channelMax;
		#endregion

		internal CancellationToken _cancellationToken;

		internal const int MaxChannels = 10;
		internal readonly Channel[] _channels = new Channel[MaxChannels + 1]; // 1-based index

		internal ConnectionIO _io;

		public Connection2()
		{
			_cancellationTokenSrc = new CancellationTokenSource();
			_cancellationToken = _cancellationTokenSrc.Token;

			_io = new ConnectionIO(this);
		}

		#region populated by server replies

		public IDictionary<string, object> ServerProperties { get { return _serverProperties; } }

		public string AuthMechanisms { get { return _mechanisms; } }
		
		public ushort HeartbeatFreqSuggested { get { return _heartbeat; } }
		
		public string KnownHosts { get { return _knownHosts; } }
		
		public ushort ChannelMax { get { return _channelMax; } }
		
		#endregion

		internal async Task Connect(string hostname, string vhost, string username, string password, int port = 5672)
		{
			await _io.InternalDoConnectSocket(hostname, port);
			await _io.Handshake(vhost, username, password);
		}

		public void Dispose()
		{
			InternalClose(initiatedByUser: true);
		}

		private void InternalClose(bool initiatedByUser)
		{
			// mark as closed
			_io.InitiateCleanClose(!initiatedByUser, 0, 0);

			// Close all channels

			// drain pending calls

			// cancel token src

			// dispose resources
			_io.Dispose();
		}

		// [MethodImpl(MethodImplOptions.AggressiveInlining)]
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
	}
}
