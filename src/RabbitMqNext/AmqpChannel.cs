namespace RabbitMqNext
{
	using System;
	using System.Collections.Concurrent;
	using System.Collections.Generic;
	using System.Threading.Tasks;
	using Internals;

	internal class AmqpChannel : IAmqpChannel
	{
		private readonly ushort _channelNum;
		private readonly ConnectionStateMachine _connection;
		internal readonly ConcurrentQueue<CommandToSend> _awaitingReplyQueue;

		private volatile int _closed = 0;

		public AmqpChannel(ushort channelNum, ConnectionStateMachine connection)
		{
			_channelNum = channelNum;
			_connection = connection;
			_awaitingReplyQueue = new ConcurrentQueue<CommandToSend>();
		}

		public int ChannelNumber { get { return _channelNum; } }

		public bool Closed { get { return _closed != 0; } }

		public Task BasicQos(uint prefetchSize, ushort prefetchCount, bool global)
		{
			var tcs = new TaskCompletionSource<bool>();

			var writer = AmqpChannelLevelFrameWriter.BasicQos(prefetchSize, prefetchCount, global);

			_connection.SendCommand(_channelNum, 60, 10, writer,
				reply: (channel, classMethodId, error) =>
				{
					if (classMethodId == AmqpClassMethodChannelLevelConstants.BasicQosOk)
					{
						_connection._frameReader.Read_BasicQosOk(() =>
						{
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

		public Task BasicAck(ulong deliveryTag, bool multiple)
		{
			throw new NotImplementedException();
//			var tcs = new TaskCompletionSource<bool>();
//			return tcs.Task;
		}

		public Task ExchangeDeclare(string exchange, string type, bool durable, bool autoDelete, IDictionary<string, object> arguments, bool waitConfirmation)
		{
			var tcs = new TaskCompletionSource<bool>();

			var writer = AmqpChannelLevelFrameWriter.ExchangeDeclare(exchange, type, durable, autoDelete, 
				arguments, false, false, waitConfirmation);

			_connection.SendCommand(_channelNum, 40, 10, writer,
				reply: (channel, classMethodId, error) =>
				{
					if (waitConfirmation && classMethodId == AmqpClassMethodChannelLevelConstants.ExchangeDeclareOk)
					{
						_connection._frameReader.Read_ExchangeDeclareOk(() =>
						{
							tcs.SetResult(true);
						});
					}
					else if (!waitConfirmation)
					{
						tcs.SetResult(true);
					}
					else
					{
						Util.SetException(tcs, error, classMethodId);
					}
				}, expectsReply: waitConfirmation);

			return tcs.Task;
		}

		public Task<AmqpQueueInfo> QueueDeclare(string queue, bool passive, bool durable, bool exclusive, bool autoDelete, IDictionary<string, object> arguments, bool waitConfirmation)
		{
			var tcs = new TaskCompletionSource<AmqpQueueInfo>();

			var writer = AmqpChannelLevelFrameWriter.QueueDeclare(queue, passive, durable, 
				exclusive, autoDelete, arguments, waitConfirmation);

			_connection.SendCommand(_channelNum, 50, 10, writer,
				reply: async (channel, classMethodId, error) =>
				{
					if (waitConfirmation && classMethodId == AmqpClassMethodChannelLevelConstants.QueueDeclareOk)
					{
						await _connection._frameReader.Read_QueueDeclareOk((queueName, messageCount, consumerCount) =>
						{
							tcs.SetResult(new AmqpQueueInfo()
							{
								Name = queueName, Consumers =  consumerCount, Messages = messageCount
							});
						});
					}
					else if (!waitConfirmation)
					{
						tcs.SetResult(new AmqpQueueInfo { Name = queue });
					}
					else
					{
						Util.SetException(tcs, error, classMethodId);
					}
				}, expectsReply: waitConfirmation);

			return tcs.Task;
		}

		public Task QueueBind(string queue, string exchange, string routingKey, IDictionary<string, object> arguments, bool waitConfirmation)
		{
			var tcs = new TaskCompletionSource<bool>();

			var writer = AmqpChannelLevelFrameWriter.QueueBind(queue, exchange, routingKey, arguments, waitConfirmation);

			_connection.SendCommand(_channelNum, 50, 20, writer,
				reply: (channel, classMethodId, error) =>
				{
					if (waitConfirmation && classMethodId == AmqpClassMethodChannelLevelConstants.QueueBindOk)
					{
						_connection._frameReader.Read_QueueBindOk(() =>
						{
							tcs.SetResult(true);
						});
					}
					else if (!waitConfirmation)
					{
						tcs.SetResult(true);
					}
					else
					{
						Util.SetException(tcs, error, classMethodId);
					}
				}, expectsReply: waitConfirmation);

			return tcs.Task;
		}

		public Task BasicPublish(string exchange, string routingKey, bool mandatory, bool immediate, BasicProperties properties, ArraySegment<byte> buffer)
		{
			properties = properties ?? new BasicProperties();

			var tcs = new TaskCompletionSource<bool>();

			var args = new FrameParameters.BasicPublishArgs()
			{
				exchange = exchange, immediate = immediate, 
				routingKey = routingKey, mandatory = mandatory,
				properties = properties, buffer = buffer
			};

			_connection.SendCommand(_channelNum, 60, 40,
				AmqpChannelLevelFrameWriter.InternalBasicPublish, reply: null, expectsReply: false, 
				tcs: tcs, optArg: args);

			return tcs.Task;
		}

		public Task Close()
		{
			if (_closed == 0) return InternalClose(true);
			
			return Task.CompletedTask;
		}

		private async Task InternalClose(bool sendClose)
		{
			if (sendClose)
				await __SendChannelClose(AmqpConstants.ReplySuccess, "bye");
			else
				await __SendChannelCloseOk();

			_connection.ChannelClosed(this);
		}

		internal Task Open()
		{
			var tcs = new TaskCompletionSource<bool>();

			var writer = AmqpChannelLevelFrameWriter.ChannelOpen();

			_connection.SendCommand(_channelNum, 20, 10, writer,
				reply: async (channel, classMethodId, error) =>
				{
					if (classMethodId == AmqpClassMethodChannelLevelConstants.ChannelOpenOk)
					{
						await _connection._frameReader.Read_ChannelOpenOk((reserved) =>
						{
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

		internal async Task DrainMethodsWithErrorAndClose(AmqpError error, ushort classId, ushort methodId)
		{
			Util.DrainMethodsWithError(_awaitingReplyQueue, error, classId, methodId);

			await InternalClose(false);
		}

		private Task __SendChannelClose(ushort replyCode, string message)
		{
			var tcs = new TaskCompletionSource<bool>();

			_connection.SendCommand(_channelNum, 20, 40, AmqpChannelLevelFrameWriter.ChannelClose,
				reply: (channel, classMethodId, error) =>
				{
					if (classMethodId == AmqpClassMethodChannelLevelConstants.ChannelCloseOk)
					{
						_connection._frameReader.Read_Channel_CloseOk(() =>
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

		private Task __SendChannelCloseOk()
		{
			var tcs = new TaskCompletionSource<bool>();

			_connection.SendCommand(_channelNum, 20, 41, AmqpChannelLevelFrameWriter.ChannelCloseOk, reply: null, expectsReply: false, tcs: tcs);

			return tcs.Task;
		}
	}
}