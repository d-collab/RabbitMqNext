namespace RabbitMqNext.Internals
{
	using System;
	using System.Collections.Concurrent;
	using System.Text;
	using System.Threading;
	using System.Threading.Tasks;


//	internal class ConnectionStateMachine : CommonCommandSender, IFrameProcessor, IDisposable
//	{
//		private readonly AmqpPrimitivesWriter _amqpWriter;
//		internal readonly CancellationToken _cancellationToken;
//
//		private readonly AutoResetEvent _commandOutboxEvent;
//		private readonly ConcurrentQueue<CommandToSend> _commandOutbox;
//
//		internal readonly FrameReader _frameReader;
//
//		private ushort _channelMax;
//		internal uint _frameMax;
//
////		private IDictionary<string, object> _serverProperties;
////		private string _mechanisms;
////		private ushort _heartbeat;
////		private string _knownHosts;
//
//		internal const int MaxChannels = 10;
//		internal readonly AmqpChannel[] _channels = new AmqpChannel[MaxChannels + 1]; // 1-based index
//
//		private readonly ObjectPool<CommandToSend> _cmdToSendObjPool;
//
//		public ConnectionStateMachine(InternalBigEndianReader reader, 
//									  AmqpPrimitivesReader amqpReader, AmqpPrimitivesWriter amqpWriter, 
//									  CancellationToken cancellationToken)
//		{
//			_amqpWriter = amqpWriter;
//			_cancellationToken = cancellationToken;
//
//			_cmdToSendObjPool = new ObjectPool<CommandToSend>(
//				() => new CommandToSend(i => _cmdToSendObjPool.PutObject(i)), 120, true);
//
//			_commandOutboxEvent = new AutoResetEvent(false);
//			_commandOutbox = new ConcurrentQueue<CommandToSend>();
//
//			_frameReader = new FrameReader(reader, amqpReader, this);
//		}
//
//		public async Task Start(string username, string password, string vhost)
//		{
//			var t2 = new Thread(ReadFramesLoop) { IsBackground = true, Name = "ReadFramesLoop" };
//			t2.Start();
//
//			await __SendGreeting();
//			await __SendConnectionStartOk(username, password);
//			await __SendConnectionTuneOk(_channelMax, _frameMax, heartbeat: 0); // disabling heartbeats for now
//			var knownHosts = await __SendConnectionOpen(vhost);
//		}
//
//		public void NotifySocketClosed()
//		{
//			var err = new AmqpError() { ReplyText = "connection closed" };
//			Volatile.Write(ref _lastError, err);
//
//			InitiateCleanClose(false, 0, 0);
//		}
//
//		/// <summary>
//		/// Callback invoked by FrameReader instance. 
//		/// we either handle it if it's a connection level message or 
//		/// dispatch to the appropriate channel.
//		/// </summary>
//		public async Task DispatchMethod(ushort channel, int classMethodId)
//		{
//			if (channel != 0)
//			{
//				await InternalDispatchMethodToChannel(channel, classMethodId);
//			}
//			else
//			{
//				await HandleFrame(classMethodId);
//			}
//		}
//
//		internal override async Task HandleFrame(int classMethodId)
//		{
//			var handled = false;
//			switch (classMethodId)
//			{
//				case AmqpClassMethodConnectionLevelConstants.ConnectionClose:
//					handled = true;
//					_frameReader.Read_ConnectionClose2(base.HandleCloseMethod);
//					break;
//
//				// TODO: support block/unblock connection msgs
//				case AmqpClassMethodConnectionLevelConstants.ConnectionBlocked:
//				case AmqpClassMethodConnectionLevelConstants.ConnectionUnblocked:
//					handled = true;	
//					break;
//
//				// Any other connection level method?
//			}
//
//			if (!handled)
//			{
//				CommandToSend sent;
//				if (_awaitingReplyQueue.TryDequeue(out sent))
//				{
//					await sent.ReplyAction3(0, classMethodId, null);
//				}
//				// else
//				{
//					// nothing was really waiting for a reply.. exception?
//				}
//			}
//		}
//
//		internal override void InitiateCleanClose(bool startedByServer, ushort offendingClassId, ushort offendingMethodId)
//		{
//			if (startedByServer)
//				__SendConnectionCloseOk(); // confirm with server we closed
//
//			Util.DrainMethodsWithError(_awaitingReplyQueue, _lastError, offendingClassId, offendingMethodId);
//		}
//
//		private Task InternalDispatchMethodToChannel(ushort channel, int classMethodId)
//		{
//			if (channel > MaxChannels)
//			{
//				// TODO: Log
//				Console.WriteLine("wtf??? " + channel);
//				throw new Exception("Unexpecte channel number " + channel);
//			}
//
//			var channelInst = _channels[channel];
//			if (channelInst == null)
//			{
//				// TODO: Log
//				throw new Exception("Channel not initialized " + channel);
//			}
//
//			return channelInst.HandleFrame(classMethodId);
//		}
//
//		public void Dispose()
//		{
//			_commandOutboxEvent.Dispose();
//		}
//
//		internal void SendCommand(ushort channel, ushort classId, ushort methodId,
//								  Action<AmqpPrimitivesWriter, ushort, ushort, ushort, object> commandWriter,
//								  Func<ushort, int, AmqpError, Task> reply, 
//								  bool expectsReply, 
//								  TaskCompletionSource<bool> tcs = null, 
//								  object optArg = null, 
//								  TaskLight tcsL = null, 
//								  Action prepare = null)
//		{
//			if (!SetErrorResultIfErrorPending(tcs, tcsL))
//			{
//				// cancelled command since we're in error. this channel should be closed. 
//				// TODO: we should log this fact
//				return;
//			}
//
//			var cmd = _cmdToSendObjPool.GetObject();
//			// var cmd = new CommandToSend(null);
//			cmd.Channel = channel;
//			cmd.ClassId = classId; 
//			cmd.MethodId = methodId;
//			cmd.ReplyAction = reply;
//			cmd.commandGenerator = commandWriter;
//			cmd.ExpectsReply = expectsReply;
//			cmd.Tcs = tcs;
//			cmd.TcsLight = tcsL;
//			cmd.OptionalArg = optArg;
//			cmd.PrepareAction = prepare;
//
//			_commandOutbox.Enqueue(cmd);
//			_commandOutboxEvent.Set();
//		}
//
//		internal void WriteCommandsToSocket()
//		{
//			try
//			{
//				_commandOutboxEvent.WaitOne();
//
//				CommandToSend cmdToSend;
//				const int maxDrainBeforeFlush = 2;
//				int iterations = 0;
//				while (iterations++ < maxDrainBeforeFlush && _commandOutbox.TryDequeue(out cmdToSend))
//				{
//					cmdToSend.Prepare();
//
//					if (cmdToSend.ExpectsReply) // enqueues as awaiting a reply from the server
//					{
//						if (cmdToSend.Channel == 0)
//							_awaitingReplyQueue.Enqueue(cmdToSend);
//						else
//							_channels[cmdToSend.Channel]._awaitingReplyQueue.Enqueue(cmdToSend);
//					}
//
//					// writes to socket
//					var frameWriter = cmdToSend.OptionalArg as IFrameContentWriter;
//					if (frameWriter != null)
//					{
//						frameWriter.Write(_amqpWriter, cmdToSend.Channel,
//											cmdToSend.ClassId, cmdToSend.MethodId, cmdToSend.OptionalArg);
//					}
//					else
//					{
//						cmdToSend.commandGenerator(_amqpWriter, cmdToSend.Channel,
//													cmdToSend.ClassId, cmdToSend.MethodId, cmdToSend.OptionalArg);
//					}
//
//					// if writing to socket is enough, set as complete
//					if (!cmdToSend.ExpectsReply)
//					{
//#pragma warning disable 4014
//						cmdToSend.ReplyAction3(0, 0, null);
//#pragma warning restore 4014
//					}
//				}
//			}
//			catch (Exception ex)
//			{
//				Console.WriteLine("WriteCommandsToSocket error " + ex);
//				// Debugger.Log(1, "error", "WriteCommandsToSocket error " + ex.Message);
//				// throw;
//			}
//		}
//
//		private void ReadFramesLoop()
//		{
//			try
//			{
//				while (!_cancellationToken.IsCancellationRequested)
//				{
//					_frameReader.ReadAndDispatch();
//					// t.Wait(_cancellationToken);
//				}
//			}
//			catch (ThreadAbortException)
//			{
//				// no-op
//			}
//			catch (Exception ex)
//			{
//				Console.WriteLine("ReadFramesLoop error " + ex.Message);
//			}
//		}
//
//		private bool SetErrorResultIfErrorPending(TaskCompletionSource<bool> tcs = null, TaskLight tcsL = null)
//		{
//			// should we just DrainMethodsWithError();
//			var er = this._lastError;
//			if (er != null)
//			{
//				if (tcs != null)
//				{
//					tcs.TrySetException(new Exception(er.ToErrorString()));
//				}
//				else if (tcsL != null)
//				{
//					tcsL.SetException(new Exception(er.ToErrorString()));
//				}
//				return false;
//			}
//
//			return true;
//		}
//
//		private Task __SendGreeting()
//		{
//			var tcs = new TaskCompletionSource<bool>();
//
//			SendCommand(0, 0, 0, 
//				AmqpConnectionFrameWriter.Greeting(),
//				reply: (channel, classMethodId, _) =>
//				{
//					if (classMethodId == AmqpClassMethodConnectionLevelConstants.ConnectionStart)
//					{
//						_frameReader.Read_ConnectionStart((versionMajor, versionMinor, serverProperties, mechanisms, locales) =>
//						{
////							_serverProperties = serverProperties;
////							_mechanisms = mechanisms;
//
//							tcs.SetResult(true);
//						});
//					}
//					else
//					{
//						// Unexpected
//						tcs.SetException(new Exception("Unexpected result. Got " + classMethodId));
//					}
//					return Task.CompletedTask;
//
//				}, expectsReply: true);
//
//			return tcs.Task;
//		}
//
//		private Task __SendConnectionTuneOk(ushort channelMax, uint frameMax, ushort heartbeat)
//		{
//			var tcs = new TaskCompletionSource<bool>();
//
//			var writer = AmqpConnectionFrameWriter.ConnectionTuneOk(channelMax, frameMax, heartbeat);
//
//			SendCommand(0, 10, 31, writer, reply: null, expectsReply: false, tcs: tcs);
//
//			return tcs.Task;
//		}
//
//		private Task<string> __SendConnectionOpen(string vhost)
//		{
//			var tcs = new TaskCompletionSource<string>();
//			var writer = AmqpConnectionFrameWriter.ConnectionOpen(vhost, string.Empty, false);
//
//			SendCommand(0, 10, 40, writer,
//				reply: (channel, classMethodId, error) =>
//				{
//					if (classMethodId == AmqpClassMethodConnectionLevelConstants.ConnectionOpenOk)
//					{
//						_frameReader.Read_ConnectionOpenOk((knowHosts) =>
//						{
//							tcs.SetResult(knowHosts);
//						});
//					}
//					else
//					{
//						Util.SetException(tcs, error, classMethodId);
//					}
//					return Task.CompletedTask;
//				}, expectsReply: true);
//
//			return tcs.Task;
//		}
//
//		private Task __SendConnectionStartOk(string username, string password)
//		{
//			var tcs = new TaskCompletionSource<bool>();
//
//			// Only supports PLAIN authentication for now
//
//			var auth = Encoding.UTF8.GetBytes("\0" + username + "\0" + password);
//			var writer = AmqpConnectionFrameWriter.ConnectionStartOk(Protocol.ClientProperties, "PLAIN", auth, "en_US");
//
//			SendCommand(0, 10, 30, writer, 
//				reply: (channel, classMethodId, error) =>
//				{
//					if (classMethodId == AmqpClassMethodConnectionLevelConstants.ConnectionTune)
//					{
//						_frameReader.Read_ConnectionTune((channelMax, frameMax, heartbeat) =>
//						{
//							_channelMax = channelMax;
//							_frameMax = frameMax;
//							// _heartbeat = heartbeat;
//
//							tcs.SetResult(true);
//						});
//					}
//					else
//					{
//						Util.SetException(tcs, error, classMethodId);
//					}
//					return Task.CompletedTask;
//				}, expectsReply: true);
//
//			return tcs.Task;
//		}
//
//		private Task __SendConnectionClose(ushort replyCode, string message)
//		{
//			var tcs = new TaskCompletionSource<bool>();
//
//			SendCommand(0, 10, 50, AmqpConnectionFrameWriter.ConnectionClose,
//				reply: (channel, classMethodId, error) =>
//				{
//					if (classMethodId == AmqpClassMethodConnectionLevelConstants.ConnectionCloseOk)
//					{
//						// _frameReader.Read_ConnectionCloseOk(() =>
//						{
//							tcs.SetResult(true);
//						}//);
//					}
//					else
//					{
//						Util.SetException(tcs, error, classMethodId);
//					}
//					return Task.CompletedTask;
//				}, 
//				expectsReply: true, 
//				optArg: new FrameParameters.CloseParams()
//				{
//					replyCode = replyCode,
//					replyText = message
//				});
//
//			return tcs.Task;
//		}
//
//		private void __SendConnectionCloseOk()
//		{
//			var tcs = new TaskCompletionSource<bool>();
//			SendCommand(0, 10, 51, AmqpConnectionFrameWriter.ConnectionCloseOk, reply: null, expectsReply: false, tcs: tcs);
////			return tcs.Task;
//		}
//
//		internal void DoCloseConnection(bool sendClose)
//		{
//			if (sendClose)
//				__SendConnectionClose(AmqpConstants.ReplySuccess, "bye");
//		}
//
//		internal void ChannelClosed(AmqpChannel amqpChannel)
//		{
//			_channels[amqpChannel.ChannelNumber] = null;
//		}
//	}
}