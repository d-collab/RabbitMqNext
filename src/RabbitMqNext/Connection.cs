namespace RabbitMqNext
{
	using System;
	using System.Diagnostics;
	using System.Net;
	using System.Net.Sockets;
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
		
		private SocketStreams _socketToStream;
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

		internal async Task Connect(string hostname, string vhost, 
									string username, string password, int port = 5672)
		{
			await InternalConnect(hostname, port);
			await InternalHandshake(username, password, vhost);
		}

		public async Task Close()
		{
			await _connectionState.DoCloseConnection(true);

			while (_socketToStream.StillSending)
			{
				await Task.Delay(TimeSpan.FromSeconds(1));
			}

			Dispose();
		}

		public async Task<IAmqpChannel> CreateChannel()
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

		private async Task InternalConnect(string hostname, int port)
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

			if (!started) 
				throw new Exception("Invalid hostname " + hostname); // ipv6 not supported yet

			_socketToStream = new SocketStreams(_socket, _cancellationTokenSrc.Token, OnSocketClosed);

			_amqpReader = new AmqpPrimitivesReader(_socketToStream.Reader);
			_amqpWriter = new AmqpPrimitivesWriter(_socketToStream.Writer, bufferPool: null, memStreamPool: null);

			_connectionState = new ConnectionStateMachine(_socketToStream.Reader, _amqpReader);
		}

		private void OnSocketClosed()
		{
			_connectionState.NotifySocketClosed();
		}

		private async Task InternalHandshake(string username, string password, string vhost)
		{
			var t1 = new Thread(WriteCommandsToSocket) { IsBackground = true, Name = "WriteCommandsToSocket" };
			t1.Start();

			var t2 = new Thread(ReadFramesLoop) { IsBackground = true, Name = "ReadFramesLoop" };
			t2.Start();

			await _connectionState.Start(username, password, vhost);
			_amqpWriter.FrameMaxSize = _connectionState._frameMax;
			_amqpReader.FrameMaxSize = _connectionState._frameMax;
		}

		private void WriteCommandsToSocket()
		{
			try
			{
				while (!_cancellationTokenSrc.IsCancellationRequested)
				{
					_connectionState._commandsToSendEvent.Wait(_cancellationTokenSrc.Token);

					CommandToSend cmdToSend;
					while (_connectionState._commandOutbox.TryDequeue(out cmdToSend))
					{
						// Console.WriteLine(" writing command ");

						if (cmdToSend.ExpectsReply)
						{
							if (cmdToSend.Channel == 0)
								_connectionState._awaitingReplyQueue.Enqueue(cmdToSend);
							else
								_connectionState._channels[cmdToSend.Channel]._awaitingReplyQueue.Enqueue(cmdToSend);
						}
						
						// writes to socket
						cmdToSend.commandGenerator(_amqpWriter, cmdToSend.Channel, 
												   cmdToSend.ClassId, cmdToSend.MethodId, cmdToSend.OptionalArg);
						
						// if writing to socket is enough, set as complete
						if (!cmdToSend.ExpectsReply)
						{
							cmdToSend.ReplyAction3(0, 0, null);
						}
					}
				}
			}
			catch (Exception ex)
			{
				Console.WriteLine("WriteCommandsToSocket error " + ex.Message);
				Debugger.Log(1, "error", "WriteCommandsToSocket error " + ex.Message);
//				throw;
			}
		}

		private void ReadFramesLoop()
		{
			try
			{
				while (!_cancellationTokenSrc.IsCancellationRequested)
				{
					var t = ReadFrame();
					t.Wait(_cancellationTokenSrc.Token);
				}
			}
			catch (ThreadAbortException)
			{
				// no-op
			}
			catch (Exception ex)
			{
				Console.WriteLine("ReadFramesLoop error " + ex.Message);
				Debugger.Log(1, "error", "ReadFramesLoop error " + ex.Message);
			}
		}

		private async Task ReadFrame()
		{
			await _connectionState._frameReader.ReadAndDispatch();
		}

		internal void Dispose()
		{
			_cancellationTokenSrc.Cancel();

			if (_socket.Connected)
			{
				_socket.Close();
			}

			_socket.Dispose();
			_connectionState.Dispose();
			_socketToStream.Dispose();
		}
	}
}