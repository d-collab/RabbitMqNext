namespace RabbitMqNext
{
	using System;
	using System.Buffers;
	using System.Collections.Concurrent;
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

	public class Connection // : FrameProcessor
	{
		private readonly CancellationTokenSource _cancellationTokenSrc;

		private Socket _socket;
		
		private SocketStreams _socketToStream;
		private AmqpPrimitivesReader _amqpReader;
		private AmqpPrimitivesWriter _amqpWriter;

		private ConnectionStateMachine _connectionState;

		public Connection()
		{
			_cancellationTokenSrc = new CancellationTokenSource();
		}

		public async Task Connect(string hostname, 
								  string vhost = "/", 
								  string username = "guest", 
								  string password = "guest", 
								  int port = 5672)
		{
			_socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp)
			{
				NoDelay = true
			};

			await InternalConnect(hostname, port);
			await InternalHandshake(username, password, vhost);
		}

		public void Close()
		{
			// Todo: clean close

			_cancellationTokenSrc.Cancel();

			_socket.Close();
		}

		private int _channelNumbers;

		public AmqpChannel CreateChannel()
		{
			var channelNum = Interlocked.Increment(ref _channelNumbers);
			return new AmqpChannel(channelNum);
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

			if (!started) throw new Exception("Invalid hostname " + hostname); // ipv6 not supported yet

			_socketToStream = new SocketStreams(_socket, _cancellationTokenSrc.Token, OnSocketClosed);

			_amqpReader = new AmqpPrimitivesReader(_socketToStream.Reader);
			_amqpWriter = new AmqpPrimitivesWriter(_socketToStream.Writer);

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
						Console.WriteLine(" writing command ");

						if (cmdToSend.ExpectsReply)
						{
							_connectionState._awaitingReplyQueue.Enqueue(cmdToSend);
						}
						
						// writes to socket
						cmdToSend.commandGenerator(_amqpWriter);
						
						// if writing to socket is enough, set as complete
						if (!cmdToSend.ExpectsReply)
						{
							cmdToSend.ReplyAction(0, 0);
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
	}
}