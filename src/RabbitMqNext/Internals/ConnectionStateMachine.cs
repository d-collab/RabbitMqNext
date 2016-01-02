namespace RabbitMqNext.Internals
{
	using System;
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

	internal class ConnectionStateMachine : FrameProcessor
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
		private uint _frameMax;
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

			DrainMethodsWithError();
		}

		private void DrainMethodsWithError()
		{
			CommandToSend sent;
			while (_awaitingReplyQueue.TryDequeue(out sent))
			{
				sent.ReplyAction(0, -1); // releases every task awaiting on this
			}
		}

		public override async Task DispatchMethod(ushort channel, int classMethodId)
		{
			CommandToSend sent;

			if (_awaitingReplyQueue.TryDequeue(out sent))
			{
				sent.ReplyAction(channel, classMethodId);
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

		private void SendCommand(Action<AmqpPrimitivesWriter> commandWriter,
								 Action<ushort, int> reply,
								 bool expectsReply)
		{
			ThrowIfErrorPending();

			// TODO: objpool
			_commandOutbox.Enqueue(new CommandToSend()
			{
				ReplyAction = reply,
				commandGenerator = commandWriter,
				ExpectsReply = expectsReply
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

			SendCommand(AmqpConnectionFrameWriter.Greeting(),
				reply: async (channel, classMethodId) =>
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

			SendCommand(AmqpConnectionFrameWriter.ConnectionTuneOk(channelMax, frameMax + 100, heartbeat),
				reply: (channel, classMethodId) =>
				{
					// there's no reply to this method, so we 
					// dont really know if it worked or not until 
					// we process the next incoming frame god knows when

					tcs.SetResult(true);

				}, expectsReply: false);

			return tcs.Task;
		}

		internal Task<string> __SendConnectionOpen(string vhost)
		{
			var tcs = new TaskCompletionSource<string>();
			var writer = AmqpConnectionFrameWriter.ConnectionOpen(vhost, string.Empty, false);

			SendCommand(writer,
				reply: async (channel, classMethodId) =>
				{
					if (classMethodId == AmqpClassMethodConstants.ConnectionOpenOk)
					{
						await _frameReader.Read_ConnectionOpenOk((knowHosts) =>
						{
							tcs.SetResult(knowHosts);
						});
					}
					else if (classMethodId == AmqpClassMethodConstants.ConnectionClose)
					{
						await _frameReader.Read_ConnectionClose((replyCode, replyText, classId, methodId) =>
						{
							tcs.SetException(new Exception("Error: " + replyText));	
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

		internal Task __SendConnectionStartOk(string username, string password)
		{
			var tcs = new TaskCompletionSource<bool>();

			// Only supports PLAIN authentication for now

			var auth = Encoding.UTF8.GetBytes("\0" + username + "\0" + password);
			var writer = AmqpConnectionFrameWriter.ConnectionStartOk(Protocol.ClientProperties, "PLAIN", auth, "en_US");

			SendCommand(writer, reply: async (channel, classMethodId) =>
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
						// Unexpected
						tcs.SetException(new Exception("Unexpected result. Got " + classMethodId));
					}
					
				}, expectsReply: true);

			return tcs.Task;
		}
	}
}