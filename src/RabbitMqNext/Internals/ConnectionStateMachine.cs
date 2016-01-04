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
		internal readonly ManualResetEventSlim _commandsToSendEvent;
		internal readonly ConcurrentQueue<CommandToSend> _commandOutbox;
		internal readonly ConcurrentQueue<CommandToSend> _awaitingReplyQueue;

		internal readonly FrameReader _frameReader;

		public ConnectionStateMachine(InternalBigEndianReader reader, 
									  AmqpPrimitivesReader amqpReader)
		{
			_commandsToSendEvent = new ManualResetEventSlim(false);
			_commandOutbox = new ConcurrentQueue<CommandToSend>();
			_awaitingReplyQueue = new ConcurrentQueue<CommandToSend>();

			_frameReader = new FrameReader(reader, amqpReader, this);
		}

		private IDictionary<string, object> _serverProperties;
		private string _mechanisms;
		private ushort _channelMax;
		internal uint _frameMax;
		private ushort _heartbeat;
		private string _knownHosts;

		// volative writes through other means
		private AmqpError _lastError;

		public async Task Start(string username, string password, string vhost)
		{
			await __SendGreeting();
			await __SendConnectionStartOk(username, password);
			await __SendConnectionTuneOk(_channelMax, _frameMax, heartbeat: 0); // disabling heartbeats for now
			_knownHosts = await __SendConnectionOpen(vhost);
		}

		public void NotifySocketClosed()
		{
			var err = new AmqpError() { ReplyText = "connection closed" };
			Volatile.Write(ref _lastError, err);

			DrainMethodsWithError(_lastError, 0, 0, 0);
		}

		private void DrainMethodsWithError(AmqpError amqpError, ushort channel, ushort classId, ushort methodId)
		{
			// releases every task awaiting
			CommandToSend sent;
			while (_awaitingReplyQueue.TryDequeue(out sent))
			{
				if (sent.Channel == channel && sent.ClassId == classId && sent.MethodId == methodId)
				{
					// if we find the "offending" command, then it gets a better error message
					sent.ReplyAction3(0, -1, amqpError); 
				}
				else
				{
					// any other task dies with a generic error.
					sent.ReplyAction3(0, -1, null);
				}
			}
		}

		public override Task DispatchCloseMethod(ushort channel, ushort replyCode, 
												 string replyText, ushort classId, ushort methodId)
		{
			DrainMethodsWithError(new AmqpError() { }, channel, classId, methodId);

			return null;
		}

		public override async Task DispatchMethod(ushort channel, int classMethodId)
		{
			CommandToSend sent;

			if (_awaitingReplyQueue.TryDequeue(out sent))
			{
				sent.ReplyAction3(channel, classMethodId, null);
			}
			else
			{
				// nothing was really waiting for a reply

				switch (classMethodId)
				{
					case AmqpClassMethodConstants.ConnectionClose:
						// we save this, and fail the next operation.
						// sad, but if you have used rabbitmq you're used to it by now

						await _frameReader.Read_ConnectionClose((replyCode, replyText, classId, methodId) =>
						{
							var error = new AmqpError()
							{
								ReplyCode = replyCode,
								ReplyText = replyText,
								ClassId = classId,
								MethodId = methodId
							};
							Volatile.Write(ref _lastError, error);
						});

						break;
					default:
						throw new Exception("DispatchMethod unexpected " + classMethodId);
				}
			}
		}

		public void Dispose()
		{
			_commandsToSendEvent.Dispose();
		}

		internal void SendCommand(ushort channel, ushort classId, ushort methodId,
								  Action<AmqpPrimitivesWriter, ushort, ushort, ushort, object> commandWriter,
								  Action<ushort, int, AmqpError> reply, bool expectsReply, 
								  TaskCompletionSource<bool> tcs = null, object optArg = null)
		{
			ThrowIfErrorPending();

			// TODO: objpool
			_commandOutbox.Enqueue(new CommandToSend()
			{
				Channel = channel,
				ClassId = classId, 
				MethodId = methodId,
				ReplyAction = reply,
				commandGenerator = commandWriter,
				ExpectsReply = expectsReply,
				Tcs = tcs,
				OptionalArg = optArg
			});
			_commandsToSendEvent.Set();
		}

		private void ThrowIfErrorPending()
		{
			// should we just DrainMethodsWithError();

			// var er = Volatile.Read(ref this._lastError); // slow. really necessary?
			var er = this._lastError;
			if (er != null) throw new Exception(er.ToErrorString());
		}

		internal Task __SendGreeting()
		{
			var tcs = new TaskCompletionSource<bool>();

			SendCommand(0, 0, 0, 
				AmqpConnectionFrameWriter.Greeting(),
				reply: async (channel, classMethodId, _) =>
				{
					if (classMethodId == AmqpClassMethodConstants.ConnectionStart)
					{
						await _frameReader.Read_ConnectionStart((versionMajor, versionMinor, serverProperties, mechanisms, locales) =>
						{
							_serverProperties = serverProperties;
							_mechanisms = mechanisms;

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

		internal Task __SendConnectionTuneOk(ushort channelMax, uint frameMax, ushort heartbeat)
		{
			var tcs = new TaskCompletionSource<bool>();

			var writer = AmqpConnectionFrameWriter.ConnectionTuneOk(channelMax, frameMax, heartbeat);

			SendCommand(0, 10, 31, writer, reply: null, expectsReply: false, tcs: tcs);

			return tcs.Task;
		}

		internal Task<string> __SendConnectionOpen(string vhost)
		{
			var tcs = new TaskCompletionSource<string>();
			var writer = AmqpConnectionFrameWriter.ConnectionOpen(vhost, string.Empty, false);

			SendCommand(0, 10, 40, writer,
				reply: async (channel, classMethodId, error) =>
				{
					if (classMethodId == AmqpClassMethodConstants.ConnectionOpenOk)
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

		internal Task __SendConnectionStartOk(string username, string password)
		{
			var tcs = new TaskCompletionSource<bool>();

			// Only supports PLAIN authentication for now

			var auth = Encoding.UTF8.GetBytes("\0" + username + "\0" + password);
			var writer = AmqpConnectionFrameWriter.ConnectionStartOk(Protocol.ClientProperties, "PLAIN", auth, "en_US");

			SendCommand(0, 10, 30, writer, 
				reply: async (channel, classMethodId, error) =>
				{
					if (classMethodId == AmqpClassMethodConstants.ConnectionTune)
					{
						await _frameReader.Read_ConnectionTune((channelMax, frameMax, heartbeat) =>
						{
							_channelMax = channelMax;
							_frameMax = frameMax;
							_heartbeat = heartbeat;

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
	}
}