namespace RabbitMqNext
{
	using System;
	using System.Collections.Concurrent;
	using System.Text;
	using System.Threading;
	using System.Threading.Tasks;
	using Internals;

	public class ConnectionIO : BaseAmqpIO, IFrameProcessor
	{
		private readonly SocketHolder _socketHolder;
		private CancellationTokenSource _connectionCancellationTokenSource;

		private AmqpPrimitivesWriter _amqpWriter;
		private AmqpPrimitivesReader _amqpReader;

		private Connection2 _conn;
		internal FrameReader _frameReader;
		internal uint _frameMax;

		private readonly AutoResetEvent _commandOutboxEvent;
		private readonly ConcurrentQueue<CommandToSend> _commandOutbox;
		private readonly ObjectPool<CommandToSend> _cmdToSendObjPool;

		public ConnectionIO(Connection2 parent)
		{
			_conn = parent;
			_socketHolder = new SocketHolder();

			_connectionCancellationTokenSource = new CancellationTokenSource();

			_commandOutboxEvent = new AutoResetEvent(false);
			_commandOutbox = new ConcurrentQueue<CommandToSend>();

			_cmdToSendObjPool = new ObjectPool<CommandToSend>(() => new CommandToSend(i => _cmdToSendObjPool.PutObject(i)), 120, true);
		}

		internal void SendCommand(ushort channel, ushort classId, ushort methodId,
			Action<AmqpPrimitivesWriter, ushort, ushort, ushort, object> commandWriter,
			Func<ushort, int, AmqpError, Task> reply,
			bool expectsReply,
			TaskCompletionSource<bool> tcs = null,
			object optArg = null,
			TaskLight tcsL = null,
			Action prepare = null)
		{
//			if (!SetErrorResultIfErrorPending(tcs, tcsL))
//			{
//				// cancelled command since we're in error. this channel should be closed. 
//				// TODO: we should log this fact
//				return;
//			}

			var cmd = _cmdToSendObjPool.GetObject();
			// var cmd = new CommandToSend(null);
			cmd.Channel = channel;
			cmd.ClassId = classId;
			cmd.MethodId = methodId;
			cmd.ReplyAction = reply;
			cmd.commandGenerator = commandWriter;
			cmd.ExpectsReply = expectsReply;
			cmd.Tcs = tcs;
			cmd.TcsLight = tcsL;
			cmd.OptionalArg = optArg;
			cmd.PrepareAction = prepare;

			_commandOutbox.Enqueue(cmd);
			_commandOutboxEvent.Set();
		}

		#region FrameProcessor

		Task IFrameProcessor.DispatchMethod(ushort channel, int classMethodId)
		{
			if (channel != 0)
			{
				return _conn.ResolveChannel(channel).HandleFrame(classMethodId);
			}

			return this.HandleFrame(classMethodId);
		}

		#endregion

		internal override Task HandleFrame(int classMethodId)
		{
			switch (classMethodId)
			{
				case AmqpClassMethodConnectionLevelConstants.ConnectionClose:
					_frameReader.Read_ConnectionClose2(base.HandleCloseMethodFromServer);
					break;

				// TODO: support block/unblock connection msgs
				case AmqpClassMethodConnectionLevelConstants.ConnectionBlocked:
				case AmqpClassMethodConnectionLevelConstants.ConnectionUnblocked:
					break;

				default:
					// Any other connection level method?
					return base.HandReplyToAwaitingQueue(classMethodId);
			}

			return Task.CompletedTask;
		}

		internal override void InitiateCleanClose(bool initiatedByServer, ushort offendingClassId, ushort offendingMethodId)
		{
			// CloseAllChannels(initiatedByServer);

			if (initiatedByServer)
				__SendConnectionCloseOk();
			else
				__SendConnectionClose(AmqpConstants.ReplySuccess, "bye");

			// TODO: drain
		}

		internal async Task InternalDoConnectSocket(string hostname, int port)
		{
			await _socketHolder.Connect(hostname, port, OnSocketClosed, OnReadyToWrite);

			_amqpWriter = new AmqpPrimitivesWriter(_socketHolder.Writer, null, null);
			_amqpReader = new AmqpPrimitivesReader(_socketHolder.Reader);

			_frameReader = new FrameReader(_socketHolder.Reader, _amqpReader, this);
		}

		internal async Task Handshake(string vhost, string username, string password)
		{
			var t2 = new Thread(ReadFramesLoop) { IsBackground = true, Name = "ReadFramesLoop" };
			t2.Start();

			await __SendGreeting();
			await __SendConnectionStartOk(username, password);
			await __SendConnectionTuneOk(_conn._channelMax, _frameMax, heartbeat: 0); // disabling heartbeats for now
			var knownHosts = await __SendConnectionOpen(vhost);
		}

		// Run on its own thread, and invokes user code from it
		private void ReadFramesLoop()
		{
			try
			{
				while (!_connectionCancellationTokenSource.Token.IsCancellationRequested)
				{
					_frameReader.ReadAndDispatch();
					// t.Wait(_cancellationToken);
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

		private void OnSocketClosed()
		{
			base.HandleDisconnect(); // either a consequence of a close method, or unexpected disconnect
		}

		public void OnReadyToWrite()
		{
			WriteCommands();
		}

		internal override void InternalDispose()
		{
			_socketHolder.Dispose();
		}

		private void WriteCommands()
		{
			try
			{
				_commandOutboxEvent.WaitOne();

				CommandToSend cmdToSend;
				const int maxDrainBeforeFlush = 2;
				int iterations = 0;
				while (iterations++ < maxDrainBeforeFlush && _commandOutbox.TryDequeue(out cmdToSend))
				{
					cmdToSend.Prepare();

					if (cmdToSend.ExpectsReply) // enqueues as awaiting a reply from the server
					{
						if (cmdToSend.Channel == 0)
							_awaitingReplyQueue.Enqueue(cmdToSend);
						else
							_conn._channels[cmdToSend.Channel]._io._awaitingReplyQueue.Enqueue(cmdToSend);
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
#pragma warning disable 4014
						cmdToSend.ReplyAction3(0, 0, null);
#pragma warning restore 4014
					}
				}
			}
			catch (Exception ex)
			{
				Console.WriteLine("WriteCommandsToSocket error " + ex);
				// Debugger.Log(1, "error", "WriteCommandsToSocket error " + ex.Message);
				// throw;
			}
		}

		private Task __SendGreeting()
		{
			var tcs = new TaskCompletionSource<bool>();

			_conn._io.SendCommand(0, 0, 0,
				AmqpConnectionFrameWriter.Greeting(),
				reply: (channel, classMethodId, _) =>
				{
					if (classMethodId == AmqpClassMethodConnectionLevelConstants.ConnectionStart)
					{
						_conn._io._frameReader.Read_ConnectionStart((versionMajor, versionMinor, serverProperties, mechanisms, locales) =>
						{
							//  _serverProperties = serverProperties;
							//  _mechanisms = mechanisms;

							tcs.SetResult(true);
						});
					}
					else
					{
						// Unexpected
						tcs.SetException(new Exception("Unexpected result. Got " + classMethodId));
					}
					return Task.CompletedTask;

				}, expectsReply: true);

			return tcs.Task;
		}

		private Task __SendConnectionTuneOk(ushort channelMax, uint frameMax, ushort heartbeat)
		{
			var tcs = new TaskCompletionSource<bool>();

			var writer = AmqpConnectionFrameWriter.ConnectionTuneOk(channelMax, frameMax, heartbeat);

			_conn._io.SendCommand(0, 10, 31, writer, reply: null, expectsReply: false, tcs: tcs);

			return tcs.Task;
		}

		private Task<string> __SendConnectionOpen(string vhost)
		{
			var tcs = new TaskCompletionSource<string>();
			var writer = AmqpConnectionFrameWriter.ConnectionOpen(vhost, string.Empty, false);

			_conn._io.SendCommand(0, 10, 40, writer,
				reply: (channel, classMethodId, error) =>
				{
					if (classMethodId == AmqpClassMethodConnectionLevelConstants.ConnectionOpenOk)
					{
						_conn._io._frameReader.Read_ConnectionOpenOk((knowHosts) =>
						{
							tcs.SetResult(knowHosts);
						});
					}
					else
					{
						BaseAmqpIO.SetException(tcs, error, classMethodId);
					}
					return Task.CompletedTask;
				}, expectsReply: true);

			return tcs.Task;
		}

		private Task __SendConnectionStartOk(string username, string password)
		{
			var tcs = new TaskCompletionSource<bool>();

			// Only supports PLAIN authentication for now

			var auth = Encoding.UTF8.GetBytes("\0" + username + "\0" + password);
			var writer = AmqpConnectionFrameWriter.ConnectionStartOk(Protocol.ClientProperties, "PLAIN", auth, "en_US");

			_conn._io.SendCommand(0, 10, 30, writer,
				reply: (channel, classMethodId, error) =>
				{
					if (classMethodId == AmqpClassMethodConnectionLevelConstants.ConnectionTune)
					{
						_conn._io._frameReader.Read_ConnectionTune((channelMax, frameMax, heartbeat) =>
						{
							_conn._channelMax = channelMax;
							_conn._heartbeat = heartbeat;
							this._frameMax = frameMax;

							tcs.SetResult(true);
						});
					}
					else
					{
						BaseAmqpIO.SetException(tcs, error, classMethodId);
					}
					return Task.CompletedTask;
				}, expectsReply: true);

			return tcs.Task;
		}

		private Task __SendConnectionClose(ushort replyCode, string message)
		{
			var tcs = new TaskCompletionSource<bool>();

			_conn._io.SendCommand(0, 10, 50, AmqpConnectionFrameWriter.ConnectionClose,
				reply: (channel, classMethodId, error) =>
				{
					if (classMethodId == AmqpClassMethodConnectionLevelConstants.ConnectionCloseOk)
					{
						// _frameReader.Read_ConnectionCloseOk(() =>
						{
							tcs.SetResult(true);
						}//);
					}
					else
					{
						BaseAmqpIO.SetException(tcs, error, classMethodId);
					}
					return Task.CompletedTask;
				},
				expectsReply: true,
				optArg: new FrameParameters.CloseParams()
				{
					replyCode = replyCode,
					replyText = message
				});

			return tcs.Task;
		}

		private void __SendConnectionCloseOk()
		{
			// var tcs = new TaskCompletionSource<bool>();
			_conn._io.SendCommand(0, 10, 51, 
				AmqpConnectionFrameWriter.ConnectionCloseOk, 
				reply: null, 
				expectsReply: false, 
				tcs: null);
			// return tcs.Task;
		}

	}
}