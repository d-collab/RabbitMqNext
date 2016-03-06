namespace RabbitMqNext.Io
{
	using System;
	using System.Collections.Concurrent;
	using System.Collections.Generic;
	using System.Text;
	using System.Threading;
	using System.Threading.Tasks;
	using RabbitMqNext;
	using RabbitMqNext.Internals;


	public class ConnectionIO : AmqpIOBase, IFrameProcessor
	{
		private readonly CancellationTokenSource _cancellationTokenSource;
		private readonly Connection _conn;
		private readonly SocketHolder _socketHolder;
		internal readonly CancellationToken _cancellationToken;

		private readonly AutoResetEvent _commandOutboxEvent;
		private readonly ConcurrentQueue<CommandToSend> _commandOutbox;
		private readonly ObjectPool<CommandToSend> _cmdToSendObjPool;
		
		private AmqpPrimitivesWriter _amqpWriter; // main output writer
		private AmqpPrimitivesReader _amqpReader; // main input reader
		internal FrameReader _frameReader; // interpreter of input

		#region populated by server replies
		public IDictionary<string, object> ServerProperties { get; private set; }
		public string AuthMechanisms { get; private set; }
		public ushort Heartbeat { get; private set; }
		public string KnownHosts { get; private set; }
		private ushort _channelMax;
		private uint _frameMax;
		#endregion
		private static int _counter = 0;

		public ConnectionIO(Connection connection) : base(channelNum: 0)
		{
			_cancellationTokenSource = new CancellationTokenSource();
			_cancellationToken = _cancellationTokenSource.Token;

			_conn = connection;
			_socketHolder = new SocketHolder(_cancellationTokenSource.Token);

			_commandOutboxEvent = new AutoResetEvent(false);
			// _commandOutboxEvent = new AutoResetSuperSlimLock(false);
			_commandOutbox = new ConcurrentQueue<CommandToSend>();

			_cmdToSendObjPool = new ObjectPool<CommandToSend>(() => new CommandToSend(i => _cmdToSendObjPool.PutObject(i)), 200, true);
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

		#region AmqpIOBase overrides

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

		internal override Task SendCloseConfirmation()
		{
			return __SendConnectionCloseOk();
		}

		internal override Task SendStartClose()
		{
			return __SendConnectionClose(AmqpConstants.ReplySuccess, "bye"); ;
		}

		internal override async Task<bool> InitiateCleanClose(bool initiatedByServer, AmqpError error)
		{
			if (!initiatedByServer)
			{
				while (!_commandOutbox.IsEmpty && !_socketHolder.IsClosed)
				{
					// give it some time to finish writing to the socket (if it's open)
					Thread.Sleep(250); 
				}
			}

			_conn.CloseAllChannels(initiatedByServer, error);

			await base.InitiateCleanClose(initiatedByServer, error).ConfigureAwait(false);

			CancelPendingCommands(error);

			_socketHolder.Close();

			return true;
		}

		internal override void InitiateAbruptClose(Exception reason)
		{
			_conn.CloseAllChannels(reason);

			CancelPendingCommands(reason);

			base.InitiateAbruptClose(reason);
		}

		protected override void InternalDispose()
		{
			_cancellationTokenSource.Cancel();
			_socketHolder.Dispose();
		}

		#endregion

		// Run on its own thread, and invokes user code from it (task continuations)
		private void WriteFramesLoop()
		{
			try
			{
				while (!this._cancellationToken.IsCancellationRequested)
				{
					_commandOutboxEvent.WaitOne(1000); // maybe it's better to _cancellationToken.Register(action) ?
//					_commandOutboxEvent.Wait();

					CommandToSend cmdToSend;
					while (_commandOutbox.TryDequeue(out cmdToSend))
					{
						cmdToSend.Prepare();

						if (cmdToSend.ExpectsReply) // enqueues as awaiting a reply from the server
						{
							var queue = cmdToSend.Channel == 0
								? _awaitingReplyQueue : 
								  _conn.ResolveChannel(cmdToSend.Channel)._awaitingReplyQueue;

							queue.Enqueue(cmdToSend);
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
							cmdToSend.RunReplyAction(0, 0, null).IntentionallyNotAwaited();
						}
					}
				}
			}
			catch (Exception ex)
			{
				Console.WriteLine("WriteCommandsToSocket error " + ex);
				this.InitiateAbruptClose(ex);
			}
		}

		// Run on its own thread, and invokes user code from it
		private void ReadFramesLoop()
		{
			try
			{
				while (!_cancellationTokenSource.Token.IsCancellationRequested)
				{
					_frameReader.ReadAndDispatch();
				}
			}
//			catch (ThreadAbortException ex)
//			{
//				// no-op
//
//				base.InitiateAbruptClose(ex);
//			}
			catch (Exception ex)
			{
				Console.WriteLine("ReadFramesLoop error " + ex.Message);

				this.InitiateAbruptClose(ex);
			}
		}

		private void OnSocketClosed()
		{
			base.HandleDisconnect(); // either a consequence of a close method, or unexpected disconnect
		}

		internal async Task<bool> InternalDoConnectSocket(string hostname, int port, bool throwOnError)
		{
			var index = Interlocked.Increment(ref _counter);

			var result = await _socketHolder.Connect(hostname, port, OnSocketClosed, index, throwOnError).ConfigureAwait(false);

			if (!throwOnError && !result) return false;

			_amqpWriter = new AmqpPrimitivesWriter(_socketHolder.Writer, null, null);
			_amqpReader = new AmqpPrimitivesReader(_socketHolder.Reader);

			_frameReader = new FrameReader(_socketHolder.Reader, _amqpReader, this);

			var t1 = new Thread(WriteFramesLoop) { IsBackground = true, Name = "WriteFramesLoop_" + index };
			t1.Start();
			var t2 = new Thread(ReadFramesLoop) { IsBackground = true, Name = "ReadFramesLoop_" + index };
			t2.Start();

			return true;
		}

		internal async Task<bool> Handshake(string vhost, string username, string password, bool throwOnError)
		{
			await __SendGreeting().ConfigureAwait(false);
			await __SendConnectionStartOk(username, password).ConfigureAwait(false);
			await __SendConnectionTuneOk(_channelMax, _frameMax, heartbeat: 0).ConfigureAwait(false); // disabling heartbeats for now
			_amqpWriter.FrameMaxSize = _frameMax;
			KnownHosts = await __SendConnectionOpen(vhost).ConfigureAwait(false);

			if (LogAdapter.ExtendedLogEnabled)
			{
				LogAdapter.LogDebug("ConnectionIO", "Known Hosts: " + KnownHosts);
			}

			return true;
		}

		internal void SendCommand(ushort channel, ushort classId, ushort methodId,
			Action<AmqpPrimitivesWriter, ushort, ushort, ushort, object> commandWriter,
			Func<ushort, int, AmqpError, Task> reply,
			bool expectsReply, TaskCompletionSource<bool> tcs = null,
			object optArg = null, TaskSlim tcsL = null, Action prepare = null)
		{
			if (_lastError != null)
			{
				// ConnClose and Channel close el al, are allowed to move fwd. 
				var cmdId = (classId << 16) | methodId;
				if (cmdId != AmqpClassMethodConnectionLevelConstants.ConnectionClose &&
				    cmdId != AmqpClassMethodConnectionLevelConstants.ConnectionCloseOk &&
					cmdId != AmqpClassMethodChannelLevelConstants.ChannelClose &&
				    cmdId != AmqpClassMethodChannelLevelConstants.ChannelCloseOk)
				{
					SetErrorResultIfErrorPending(expectsReply, reply, tcs, tcsL); 
					return;
				}
			}

			var cmd = _cmdToSendObjPool.GetObject();
			// var cmd = new CommandToSend(null);
			cmd.Channel = channel;
			cmd.ClassId = classId;
			cmd.MethodId = methodId;
			cmd.ReplyAction = reply;
			cmd.commandGenerator = commandWriter;
			cmd.ExpectsReply = expectsReply;
			cmd.Tcs = tcs;
			cmd.TcsSlim = tcsL;
			cmd.OptionalArg = optArg;
			cmd.PrepareAction = prepare;

			_commandOutbox.Enqueue(cmd);
			_commandOutboxEvent.Set();
		}

		private void SetErrorResultIfErrorPending(bool expectsReply, Func<ushort, int, AmqpError, Task> replyFn, 
												  TaskCompletionSource<bool> tcs, TaskSlim taskSlim)
		{
			if (expectsReply)
			{
				replyFn(0, 0, _lastError);
			}
			else
			{
				AmqpIOBase.SetException(tcs, _lastError, 0);
				AmqpIOBase.SetException(taskSlim, _lastError, 0);
			}
		}

		private void CancelPendingCommands(Exception reason)
		{
			CancelPendingCommands(new AmqpError() { ReplyText = reason.Message });
		}

		private void CancelPendingCommands(AmqpError error)
		{
			CommandToSend cmdToSend;
			while (_commandOutbox.TryDequeue(out cmdToSend))
			{
#pragma warning disable 4014
				cmdToSend.RunReplyAction(0, 0, error);
#pragma warning restore 4014
			}
		}

		#region Commands writing methods

		private Task __SendGreeting()
		{
			var tcs = new TaskCompletionSource<bool>();

			SendCommand(0, 0, 0,
				AmqpConnectionFrameWriter.Greeting(),
				reply: (channel, classMethodId, _) =>
				{
					if (classMethodId == AmqpClassMethodConnectionLevelConstants.ConnectionStart)
					{
						_frameReader.Read_ConnectionStart((versionMajor, versionMinor, serverProperties, mechanisms, locales) =>
						{
							this.ServerProperties = serverProperties;
							this.AuthMechanisms = mechanisms;

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

			SendCommand(0, 10, 31, writer, reply: null, expectsReply: false, tcs: tcs);

			return tcs.Task;
		}

		private Task<string> __SendConnectionOpen(string vhost)
		{
			var tcs = new TaskCompletionSource<string>();
			var writer = AmqpConnectionFrameWriter.ConnectionOpen(vhost, string.Empty, false);

			SendCommand(0, 10, 40, writer,
				reply: (channel, classMethodId, error) =>
				{
					if (classMethodId == AmqpClassMethodConnectionLevelConstants.ConnectionOpenOk)
					{
						_frameReader.Read_ConnectionOpenOk((knowHosts) =>
						{
							tcs.SetResult(knowHosts);
						});
					}
					else
					{
						AmqpIOBase.SetException(tcs, error, classMethodId);
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

			SendCommand(0, 10, 30, 
				writer,
				reply: (channel, classMethodId, error) =>
				{
					if (classMethodId == AmqpClassMethodConnectionLevelConstants.ConnectionTune)
					{
						_frameReader.Read_ConnectionTune((channelMax, frameMax, heartbeat) =>
						{
							this._channelMax = channelMax;
							this._frameMax = frameMax;
							this.Heartbeat = heartbeat;

							tcs.SetResult(true);
						});
					}
					else
					{
						AmqpIOBase.SetException(tcs, error, classMethodId);
					}
					return Task.CompletedTask;
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
						// _frameReader.Read_ConnectionCloseOk(() =>
						{
							tcs.SetResult(true);
						}//);
					}
					else
					{
						AmqpIOBase.SetException(tcs, error, classMethodId);
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

		private Task __SendConnectionCloseOk()
		{
			var tcs = new TaskCompletionSource<bool>();

			this.SendCommand(0, 10, 51,
				AmqpConnectionFrameWriter.ConnectionCloseOk,
				reply: null, expectsReply: false, tcs: tcs);

			return tcs.Task;
		}

		#endregion
	}
}