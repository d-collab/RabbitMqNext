namespace RabbitMqNext
{
	using System;
	using System.Diagnostics;
	using System.Net;
	using System.Net.Sockets;
	using System.Runtime.CompilerServices;
	using System.Threading;
	using System.Threading.Tasks;
	using Internals;

	// Connect
	// async socket, no lock, buffer poll
	// Declares
	// Publish
	// Consume
	// + support recover
	// + support multiple hosts

	public class Connection
	{
		private readonly CancellationTokenSource _cancellationTokenSrc;

		private readonly Socket _socket;
		
		private SocketRingBuffers _socketToRingBuffer;
		private AmqpPrimitivesReader _amqpReader;
		private AmqpPrimitivesWriter _amqpWriter;

		private ConnectionStateMachine _connectionState;

		private int _channelNumbers;
		
		public Connection()
		{
			_cancellationTokenSrc = new CancellationTokenSource();

			_socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp)
			{
				NoDelay = true
			};
		}

		public async Task<AmqpChannel> CreateChannel()
		{
			var channelNum = (ushort) Interlocked.Increment(ref _channelNumbers);
			
			if (channelNum > ConnectionStateMachine.MaxChannels) 
				throw new Exception("Exceeded channel limits");

			var channel = new AmqpChannel(channelNum, _connectionState);

			try
			{
				_connectionState._channels[channelNum] = channel;
				await channel.Open();
				return channel;
			}
			catch
			{
				_connectionState._channels[channelNum] = null;
				throw;
			}
		}

		public async Task Close()
		{
			await _connectionState.DoCloseConnection(true);

			while (_socketToRingBuffer.StillSending)
			{
				await Task.Delay(TimeSpan.FromSeconds(1));
			}

			Dispose();
		}

		internal async Task Connect(string hostname, string vhost, string username, string password, int port = 5672)
		{
			await InternalConnectAndHandshake(hostname, port, username, password, vhost);
		}

		private async Task InternalConnectAndHandshake(string hostname, int port, string username, string password, string vhost)
		{
			await InternalDoConnectSocket(hostname, port);

			_socketToRingBuffer = new SocketRingBuffers(_socket, _cancellationTokenSrc.Token, OnSocketClosed);
			_amqpReader = new AmqpPrimitivesReader(_socketToRingBuffer.Reader);
			_amqpWriter = new AmqpPrimitivesWriter(_socketToRingBuffer.Writer, bufferPool: null, memStreamPool: null);

			_connectionState = new ConnectionStateMachine(_socketToRingBuffer.Reader, _amqpReader, _amqpWriter, _cancellationTokenSrc.Token);

			await _connectionState.Start(username, password, vhost);
			_amqpWriter.FrameMaxSize = _connectionState._frameMax;
			_amqpReader.FrameMaxSize = _connectionState._frameMax;
		}

		private async Task InternalDoConnectSocket(string hostname, int port)
		{
			var addresses = Dns.GetHostAddresses(hostname);
			var started = false;

			foreach (var ipAddress in addresses)
			{
				if (ipAddress.AddressFamily == AddressFamily.InterNetwork)
				{
					started = true;
					await _socket.ConnectTaskAsync(new IPEndPoint(ipAddress, port));
					break;
				}
			}

			if (!started) throw new Exception("Invalid hostname " + hostname); // ipv6 not supported yet
		}

		private void OnSocketClosed()
		{
			_connectionState.NotifySocketClosed();
		}

		internal void Dispose()
		{
			_cancellationTokenSrc.Cancel();

			if (_socket.Connected)
			{
				_socket.Close();
			}

			_socket.Dispose();

			if (_connectionState != null)
				_connectionState.Dispose();

			if (_socketToRingBuffer != null)
				_socketToRingBuffer.Dispose();
		}
	}
}