namespace RabbitMqNext.Io
{
	using System;
	using System.Collections.Concurrent;
	using System.Collections.Generic;
	using System.Text;
	using System.Threading;
	using System.Threading.Tasks;
	using Internals.RingBuffer;
	using RabbitMqNext;
	using RabbitMqNext.Internals;
	using Recovery;


	public sealed class ConnectionIO : AmqpIOBase, IFrameProcessor
	{
		private static int _counter = 0;

		private readonly Connection _conn;
		
		private readonly AutoResetEvent _commandOutboxEvent;
		private readonly ManualResetEventSlim _waitingServerReply;
		private readonly ConcurrentQueue<CommandToSend> _commandOutbox;
		private readonly ObjectPool<CommandToSend> _cmdToSendObjPool;
		
		internal readonly SocketHolder _socketHolder;

		private readonly AmqpPrimitivesWriter _amqpWriter; // main output writer
		private readonly AmqpPrimitivesReader _amqpReader; // main input reader
		internal readonly FrameReader _frameReader; // interpreter of input

		private CancellationTokenSource _threadCancelSource;
		private CancellationToken _threadCancelToken;

		#region populated by server replies

		public IDictionary<string, object> ServerProperties { get; private set; }
		public string AuthMechanisms { get; private set; }
		public ushort Heartbeat { get; private set; }
		public string KnownHosts { get; private set; }
		private ushort _channelMax;
		private uint _frameMax;
		
		#endregion
		
		public ConnectionIO(Connection connection) : base(channelNum: 0)
		{
			_conn = connection;
			_socketHolder = new SocketHolder();

			_commandOutboxEvent = new AutoResetEvent(false);
			_waitingServerReply = new ManualResetEventSlim(true);
			// _commandOutboxEvent = new AutoResetSuperSlimLock(false);
			_commandOutbox = new ConcurrentQueue<CommandToSend>();

			_cmdToSendObjPool = new ObjectPool<CommandToSend>(() => new CommandToSend(i => _cmdToSendObjPool.PutObject(i)), 200, true);

			_amqpWriter = new AmqpPrimitivesWriter();
			_amqpReader = new AmqpPrimitivesReader();
			_frameReader = new FrameReader();
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
					_frameReader.Read_ConnectionClose(base.HandleCloseMethodFromServer);
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
				_conn.NotifyClosedByUser();

				while (!_commandOutbox.IsEmpty && !_socketHolder.IsClosed)
				{
					// give it some time to finish writing to the socket (if it's open)
					Thread.Sleep(250);
				}
			}
			else
			{
				if (_conn.NotifyClosedByServer() == RecoveryAction.WillReconnect)
				{
					CancelPendingCommands(error);

					return false;
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

			if (_conn.NotifyAbruptClose(reason) == RecoveryAction.WillReconnect)
			{
				CancelPendingCommands(reason);
			}
			else
			{
				base.InitiateAbruptClose(reason);
			}
		}

		protected override void InternalDispose()
		{
			// _cancellationTokenSource.Cancel();
			_socketHolder.Dispose();
		}

		#endregion

		private void OnSocketClosed()
		{
			if (_threadCancelSource != null)
			{
				_threadCancelSource.Cancel();
				_commandOutboxEvent.Set(); // <- gets it out of the Wait state, and fast exit
			}

			base.HandleDisconnect(); // either a consequence of a close method, or unexpected disconnect
		}

		internal async Task<bool> InternalDoConnectSocket(string hostname, int port, bool throwOnError)
		{
			var index = Interlocked.Increment(ref _counter);

			var result = await _socketHolder.Connect(hostname, port, index, throwOnError).ConfigureAwait(false);

			if (!throwOnError && !result)
			{
				Interlocked.Decrement(ref _counter);
				return false;
			}

			// Reset state
			_lastError = null;
			while (!_commandOutbox.IsEmpty)
			{
				CommandToSend cmd;
				_commandOutbox.TryDequeue(out cmd);
			}

			if (_threadCancelSource != null)
			{
				_threadCancelSource.Dispose();
			}

			_threadCancelSource = new CancellationTokenSource();
			_threadCancelToken = _threadCancelSource.Token;

			_socketHolder.WireStreams(_threadCancelToken, OnSocketClosed);

			_amqpWriter.Initialize(_socketHolder.Writer);
			_amqpReader.Initialize(_socketHolder.Reader);
			_frameReader.Initialize(_socketHolder.Reader, _amqpReader, this);

			ThreadFactory.BackgroundThread(WriteFramesLoop, "WriteFramesLoop_" + index);
			ThreadFactory.BackgroundThread(ReadFramesLoop,  "ReadFramesLoop_"  + index);

			return true;
		}

		internal async Task<bool> Handshake(string vhost, string username, string password, string connectionName, bool throwOnError)
		{
			await __SendGreeting().ConfigureAwait(false);
			await __SendConnectionStartOk(username, password, connectionName).ConfigureAwait(false);
			await __SendConnectionTuneOk(_channelMax, _frameMax, heartbeat: 0).ConfigureAwait(false); // disabling heartbeats for now
			_amqpWriter.FrameMaxSize = _frameMax;
			KnownHosts = await __SendConnectionOpen(vhost).ConfigureAwait(false);

			if (LogAdapter.ExtendedLogEnabled)
				LogAdapter.LogDebug("ConnectionIO", "Known Hosts: " + KnownHosts);

			return true;
		}

		// Run on its own thread, and invokes user code from it (task continuations)
		private void WriteFramesLoop()
		{
			if (LogAdapter.ExtendedLogEnabled)
				LogAdapter.LogDebug("ConnectionIO", "WriteFramesLoop starting");

			CommandToSend cmdToSend = null;

			try
			{
				var token = _threadCancelToken;

				while (!token.IsCancellationRequested)
				{
					_commandOutboxEvent.WaitOne(1000); // maybe it's better to _cancellationToken.Register(action) ?

					while (_commandOutbox.TryDequeue(out cmdToSend))
					{
						_waitingServerReply.Wait(token); // Contention sadly required by the server/amqp

						// The command will signal that we can send more commands...
						cmdToSend.Prepare(cmdToSend.ExpectsReply ? _waitingServerReply : null);

						if (cmdToSend.ExpectsReply) // enqueues as awaiting a reply from the server
						{
							_waitingServerReply.Reset(); // cannot send anything else

							var queue = cmdToSend.Channel == 0
								? _awaitingReplyQueue : 
								  _conn.ResolveChannel(cmdToSend.Channel)._awaitingReplyQueue;

							queue.Enqueue(cmdToSend);
						}

						// writes to socket
						var frameWriter = cmdToSend.OptionalArg as IFrameContentWriter;
						if (frameWriter != null)
						{
							frameWriter.Write(_amqpWriter, cmdToSend.Channel, cmdToSend.ClassId, cmdToSend.MethodId, cmdToSend.OptionalArg);
						}
						else
						{
							cmdToSend.commandGenerator(_amqpWriter, cmdToSend.Channel, cmdToSend.ClassId, cmdToSend.MethodId, cmdToSend.OptionalArg);
						}

						// if writing to socket is enough, set as complete
						if (!cmdToSend.ExpectsReply)
						{
							cmdToSend.RunReplyAction(0, 0, null).IntentionallyNotAwaited();
						}
					}
				}

				LogAdapter.LogError("ConnectionIO", "WriteFramesLoop exiting");
			}
			catch (ThreadAbortException)
			{
				// no-op
			}
			catch (Exception ex)
			{
				LogAdapter.LogError("ConnectionIO", "WriteFramesLoop error. Last command " + cmdToSend.ToDebugInfo(), ex);

				this.InitiateAbruptClose(ex);
			}
		}

		// Run on its own thread, and invokes user code from it
		private void ReadFramesLoop()
		{
			if (LogAdapter.ExtendedLogEnabled)
				LogAdapter.LogDebug("ConnectionIO", "ReadFramesLoop starting");

			try
			{
				var token = _threadCancelToken;

				while (!token.IsCancellationRequested)
				{
					_frameReader.ReadAndDispatch();
				}

				LogAdapter.LogError("ConnectionIO", "ReadFramesLoop exiting");
			}
			catch (ThreadAbortException)
			{
				// no-op
			}
			catch (Exception ex)
			{
				LogAdapter.LogError("ConnectionIO", "ReadFramesLoop error", ex);

				this.InitiateAbruptClose(ex);
			}
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
				cmdToSend.RunReplyAction(0, 0, error).IntentionallyNotAwaited();
			}
		}

		#region Commands writing methods

		private Task __SendGreeting()
		{
			if (LogAdapter.ProtocolLevelLogEnabled)
				LogAdapter.LogDebug("ConnectionIO", "__SendGreeting >");

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

							if (LogAdapter.ProtocolLevelLogEnabled)
								LogAdapter.LogDebug("ConnectionIO", "__SendGreeting completed");

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

			if (LogAdapter.ProtocolLevelLogEnabled)
				LogAdapter.LogDebug("ConnectionIO", "__SendConnectionTuneOk >");

			return tcs.Task;
		}

		private Task<string> __SendConnectionOpen(string vhost)
		{
			if (LogAdapter.ProtocolLevelLogEnabled)
				LogAdapter.LogDebug("ConnectionIO", "__SendConnectionOpen > vhost " + vhost);

			var tcs = new TaskCompletionSource<string>();
			var writer = AmqpConnectionFrameWriter.ConnectionOpen(vhost, string.Empty, false);

			SendCommand(0, 10, 40, writer,
				reply: (channel, classMethodId, error) =>
				{
					if (classMethodId == AmqpClassMethodConnectionLevelConstants.ConnectionOpenOk)
					{
						_frameReader.Read_ConnectionOpenOk((knowHosts) =>
						{
							if (LogAdapter.ProtocolLevelLogEnabled)
								LogAdapter.LogDebug("ConnectionIO", "__SendConnectionOpen completed for vhost " + vhost);

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

		private Task __SendConnectionStartOk(string username, string password, string connectionName)
		{
			if (LogAdapter.ProtocolLevelLogEnabled)
				LogAdapter.LogDebug("ConnectionIO", "__SendConnectionStartOk >");

			var tcs = new TaskCompletionSource<bool>();

			// Only supports PLAIN authentication for now

			var clientProperties = Protocol.ClientProperties;
			if (!string.IsNullOrEmpty(connectionName))
			{
				clientProperties = new Dictionary<string, object>(clientProperties);
				clientProperties["connection_name"] = connectionName;
			}

			var auth = Encoding.UTF8.GetBytes("\0" + username + "\0" + password);
			var writer = AmqpConnectionFrameWriter.ConnectionStartOk(clientProperties, "PLAIN", auth, "en_US");

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

							if (LogAdapter.ProtocolLevelLogEnabled)
								LogAdapter.LogDebug("ConnectionIO", "__SendConnectionStartOk completed.");

							LogAdapter.LogDebug("ConnectionIO", "Tune results: Channel max: " + channel + " Frame max size: " + frameMax + " heartbeat: " + heartbeat);

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
			if (LogAdapter.ProtocolLevelLogEnabled)
				LogAdapter.LogDebug("ConnectionIO", "__SendConnectionClose >");

			var tcs = new TaskCompletionSource<bool>();

			SendCommand(0, 10, 50, AmqpConnectionFrameWriter.ConnectionClose,
				reply: (channel, classMethodId, error) =>
				{
					if (classMethodId == AmqpClassMethodConnectionLevelConstants.ConnectionCloseOk)
					{
						if (LogAdapter.ProtocolLevelLogEnabled)
							LogAdapter.LogDebug("ConnectionIO", "__SendConnectionClose enabled");
						
						tcs.SetResult(true);
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
			if (LogAdapter.ProtocolLevelLogEnabled)
				LogAdapter.LogDebug("ConnectionIO", "__SendConnectionCloseOk >");

			var tcs = new TaskCompletionSource<bool>();

			this.SendCommand(0, 10, 51,
				AmqpConnectionFrameWriter.ConnectionCloseOk,
				reply: null, expectsReply: false, tcs: tcs);

			return tcs.Task;
		}

		#endregion
	}
}