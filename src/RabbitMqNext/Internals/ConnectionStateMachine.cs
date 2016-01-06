namespace RabbitMqNext.Internals
{
	using System;
	using System.Buffers;
	using System.Collections.Concurrent;
	using System.Collections.Generic;
	using System.Text;
	using System.Threading;
	using System.Threading.Tasks;


	// State machine is:
	// greeting -> [connectionStart | close]
	// connectionStart sends connectionStartOk [connectionTune | close]
	// connectionTune sends connectionTuneOk [Tuned | close]
	// Tuned sends ConnectionOpen [ConnectionOpenOk | close]
	// ConnectionOpenOk -> Opened

	internal class ConnectionStateMachine : FrameProcessor, IDisposable
	{
		private readonly AmqpPrimitivesWriter _amqpWriter;
		private readonly CancellationToken _cancellationToken;

		private readonly ManualResetEventSlim _commandsToSendEvent;
		private readonly ConcurrentQueue<CommandToSend> _commandOutbox;
		private readonly ConcurrentQueue<CommandToSend> _awaitingReplyQueue;

		internal readonly FrameReader _frameReader;

		private AmqpError _lastError; // volative writes through other means

		private ushort _channelMax;
		internal uint _frameMax;

//		private IDictionary<string, object> _serverProperties;
//		private string _mechanisms;
//		private ushort _heartbeat;
//		private string _knownHosts;

		internal const int MaxChannels = 10;
		internal readonly AmqpChannel[] _channels = new AmqpChannel[MaxChannels + 1]; // 1-based index

		private readonly ObjectPool<CommandToSend> _cmdToSendObjPool;


		public ConnectionStateMachine(InternalBigEndianReader reader, 
			AmqpPrimitivesReader amqpReader, 
			AmqpPrimitivesWriter amqpWriter, 
			CancellationToken cancellationToken)
		{
			_amqpWriter = amqpWriter;
			_cancellationToken = cancellationToken;

			_cmdToSendObjPool = new ObjectPool<CommandToSend>(
				() => new CommandToSend(i => _cmdToSendObjPool.PutObject(i)), 100, true);

			// _commandsToSendEvent = new AutoResetEvent(false);
			_commandsToSendEvent = new ManualResetEventSlim(false);
			_commandOutbox = new ConcurrentQueue<CommandToSend>();
			_awaitingReplyQueue = new ConcurrentQueue<CommandToSend>();

			_frameReader = new FrameReader(reader, amqpReader, this);
		}

		public async Task Start(string username, string password, string vhost)
		{
			var t1 = new Thread(WriteCommandsToSocket) { IsBackground = true, Name = "WriteCommandsToSocket" };
			t1.Start();

			var t2 = new Thread(ReadFramesLoop) { IsBackground = true, Name = "ReadFramesLoop" };
			t2.Start();

			await __SendGreeting();
			await __SendConnectionStartOk(username, password);
			await __SendConnectionTuneOk(_channelMax, _frameMax, heartbeat: 0); // disabling heartbeats for now
			var knownHosts = await __SendConnectionOpen(vhost);
		}

		public void NotifySocketClosed()
		{
			var err = new AmqpError() { ReplyText = "connection closed" };
			Volatile.Write(ref _lastError, err);

			DrainMethodsWithErrorAndClose(_lastError, 0, 0);
		}

		public override async Task DispatchMethod(ushort channel, int classMethodId)
		{
			if (channel != 0)
			{
				await InternalDispatchMethodToChannel(channel, classMethodId);
			}
			else
			{
				await InternalDispatchMethodToConnection(channel, classMethodId);
			}
		}

		public override async Task DispatchCloseMethod(ushort channel, ushort replyCode, string replyText, ushort classId, ushort methodId)
		{
			var error = new AmqpError() {ClassId = classId, MethodId = methodId, ReplyCode = replyCode, ReplyText = replyText};
			await DrainMethodsWithErrorAndClose(error, classId, methodId);
		}

		public override async Task DispatchChannelCloseMethod(ushort channel, ushort replyCode, string replyText, ushort classId, ushort methodId)
		{
			var error = new AmqpError() { ClassId = classId, MethodId = methodId, ReplyCode = replyCode, ReplyText = replyText };
			var channelInst = _channels[channel];
			await channelInst.DrainMethodsWithErrorAndClose(error, classId, methodId);
		}

		private Task InternalDispatchMethodToConnection(ushort channel, int classMethodId)
		{
			CommandToSend sent;
			if (_awaitingReplyQueue.TryDequeue(out sent))
			{
				sent.ReplyAction3(channel, classMethodId, null);
			}
			else
			{
				// nothing was really waiting for a reply
			}

			return Task.CompletedTask;
		}

		private async Task InternalDispatchMethodToChannel(ushort channel, int classMethodId)
		{
			if (channel > MaxChannels)
			{
				Console.WriteLine("wtf??? " + channel);
				throw new Exception("Unexpecte channel number " + channel);
			}

			var channelInst = _channels[channel];
			if (channelInst == null)
				throw new Exception("Channel not initialized " + channel);

			CommandToSend cmdAwaiting;
			if (classMethodId == AmqpClassMethodChannelLevelConstants.BasicDeliver)
			{
				await channelInst.DispatchMethod(channel, classMethodId);
			}
			else if (channelInst._awaitingReplyQueue.TryDequeue(out cmdAwaiting))
			{
				cmdAwaiting.ReplyAction3(channel, classMethodId, null);
			}
			else
			{
				await channelInst.DispatchMethod(channel, classMethodId);
			}
		}

		public void Dispose()
		{
			_commandsToSendEvent.Dispose();
		}

		internal void SendCommand(ushort channel, ushort classId, ushort methodId,
								  Action<AmqpPrimitivesWriter, ushort, ushort, ushort, object> commandWriter,
								  Action<ushort, int, AmqpError> reply, bool expectsReply, 
								  TaskCompletionSource<bool> tcs = null, object optArg = null, TaskLight tcsL = null)
		{
			ThrowIfErrorPending();

			var cmd = _cmdToSendObjPool.GetObject();
			cmd.Channel = channel;
			cmd.ClassId = classId; 
			cmd.MethodId = methodId;
			cmd.ReplyAction = reply;
			cmd.commandGenerator = commandWriter;
			cmd.ExpectsReply = expectsReply;
			cmd.Tcs = tcs;
			cmd.TcsLight = tcsL;
			cmd.OptionalArg = optArg;

			_commandOutbox.Enqueue(cmd);

			_commandsToSendEvent.Set();
		}

		private void WriteCommandsToSocket()
		{
			try
			{
				while (!_cancellationToken.IsCancellationRequested)
				{
					_commandsToSendEvent.Wait(_cancellationToken);
					_commandsToSendEvent.Reset();

					CommandToSend cmdToSend;
					while (_commandOutbox.TryDequeue(out cmdToSend))
					{
						// Console.WriteLine(" writing command ");

						if (cmdToSend.ExpectsReply)
						{
							if (cmdToSend.Channel == 0)
								_awaitingReplyQueue.Enqueue(cmdToSend);
							else
								_channels[cmdToSend.Channel]._awaitingReplyQueue.Enqueue(cmdToSend);
						}

						// writes to socket
						var frameWriter = cmdToSend.OptionalArg as IFrameContentWriter;
						if (frameWriter != null)
						{
							frameWriter.Write(_amqpWriter, cmdToSend.Channel,
											  cmdToSend.ClassId, cmdToSend.MethodId, cmdToSend.OptionalArg);
						}
						else
						{
							cmdToSend.commandGenerator(_amqpWriter, cmdToSend.Channel,
													   cmdToSend.ClassId, cmdToSend.MethodId, cmdToSend.OptionalArg);
						}

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
				// Debugger.Log(1, "error", "WriteCommandsToSocket error " + ex.Message);
				// throw;
			}
		}

		private void ReadFramesLoop()
		{
			try
			{
				while (!_cancellationToken.IsCancellationRequested)
				{
					var t = ReadFrame();
					t.Wait(_cancellationToken);
				}
			}
			catch (ThreadAbortException)
			{
				// no-op
			}
			catch (Exception ex)
			{
				Console.WriteLine("ReadFramesLoop error " + ex.Message);
			}
		}

		private async Task ReadFrame()
		{
			await _frameReader.ReadAndDispatch();
		}

		private void ThrowIfErrorPending()
		{
			// should we just DrainMethodsWithError();

			// var er = Volatile.Read(ref this._lastError); // slow. really necessary?
			var er = this._lastError;
			if (er != null) throw new Exception(er.ToErrorString());
		}

		private async Task DrainMethodsWithErrorAndClose(AmqpError amqpError, ushort classId, ushort methodId)
		{
			Util.DrainMethodsWithError(_awaitingReplyQueue, amqpError, classId, methodId);

			await __SendConnectionCloseOk();
		}

		private Task __SendGreeting()
		{
			var tcs = new TaskCompletionSource<bool>();

			SendCommand(0, 0, 0, 
				AmqpConnectionFrameWriter.Greeting(),
				reply: async (channel, classMethodId, _) =>
				{
					if (classMethodId == AmqpClassMethodConnectionLevelConstants.ConnectionStart)
					{
						await _frameReader.Read_ConnectionStart((versionMajor, versionMinor, serverProperties, mechanisms, locales) =>
						{
//							_serverProperties = serverProperties;
//							_mechanisms = mechanisms;

							tcs.SetResult(true);
						});
					}
					else
					{
						// Unexpected
						tcs.SetException(new Exception("Unexpected result. Got " + classMethodId));
					}

				}, expectsReply: true);

			return tcs.Task;
		}

		private Task __SendConnectionTuneOk(ushort channelMax, uint frameMax, ushort heartbeat)
		{
			var tcs = new TaskCompletionSource<bool>();

			var writer = AmqpConnectionFrameWriter.ConnectionTuneOk(channelMax, frameMax, heartbeat);

			SendCommand(0, 10, 31, writer, reply: null, expectsReply: false, tcs: tcs);

			return tcs.Task;
		}

		private Task<string> __SendConnectionOpen(string vhost)
		{
			var tcs = new TaskCompletionSource<string>();
			var writer = AmqpConnectionFrameWriter.ConnectionOpen(vhost, string.Empty, false);

			SendCommand(0, 10, 40, writer,
				reply: async (channel, classMethodId, error) =>
				{
					if (classMethodId == AmqpClassMethodConnectionLevelConstants.ConnectionOpenOk)
					{
						await _frameReader.Read_ConnectionOpenOk((knowHosts) =>
						{
							tcs.SetResult(knowHosts);
						});
					}
					else
					{
						Util.SetException(tcs, error, classMethodId);
					}

				}, expectsReply: true);

			return tcs.Task;
		}

		private Task __SendConnectionStartOk(string username, string password)
		{
			var tcs = new TaskCompletionSource<bool>();

			// Only supports PLAIN authentication for now

			var auth = Encoding.UTF8.GetBytes("\0" + username + "\0" + password);
			var writer = AmqpConnectionFrameWriter.ConnectionStartOk(Protocol.ClientProperties, "PLAIN", auth, "en_US");

			SendCommand(0, 10, 30, writer, 
				reply: async (channel, classMethodId, error) =>
				{
					if (classMethodId == AmqpClassMethodConnectionLevelConstants.ConnectionTune)
					{
						await _frameReader.Read_ConnectionTune((channelMax, frameMax, heartbeat) =>
						{
							_channelMax = channelMax;
							_frameMax = frameMax;
							// _heartbeat = heartbeat;

							tcs.SetResult(true);
						});
					}
					else
					{
						Util.SetException(tcs, error, classMethodId);
					}
					
				}, expectsReply: true);

			return tcs.Task;
		}

		private Task __SendConnectionClose(ushort replyCode, string message)
		{
			var tcs = new TaskCompletionSource<bool>();

			SendCommand(0, 10, 50, AmqpConnectionFrameWriter.ConnectionClose,
				reply: (channel, classMethodId, error) =>
				{
					if (classMethodId == AmqpClassMethodConnectionLevelConstants.ConnectionCloseOk)
					{
						_frameReader.Read_ConnectionCloseOk(() =>
						{
							tcs.SetResult(true);
						});
					}
					else
					{
						Util.SetException(tcs, error, classMethodId);
					}

				}, 
				expectsReply: true, 
				optArg: new FrameParameters.CloseParams()
				{
					replyCode = replyCode,
					replyText = message
				});

			return tcs.Task;
		}

		private Task __SendConnectionCloseOk()
		{
			var tcs = new TaskCompletionSource<bool>();

			SendCommand(0, 10, 51, AmqpConnectionFrameWriter.ConnectionCloseOk, reply: null, expectsReply: false, tcs: tcs);

			return tcs.Task;
		}

		internal async Task DoCloseConnection(bool sendClose)
		{
			if (sendClose)
				await __SendConnectionClose(AmqpConstants.ReplySuccess, "bye");
			 
				
		}

		internal void ChannelClosed(AmqpChannel amqpChannel)
		{
			_channels[amqpChannel.ChannelNumber] = null;
		}
	}
}