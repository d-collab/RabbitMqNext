namespace RabbitMqNext
{
	using System;
	using System.Collections.Generic;
	using System.IO;
	using System.Threading.Tasks;
	using Internals;

	public class ChannelIO : AmqpIOBase
	{
		private readonly Channel _channel;
		private readonly ushort _channelNum;
		internal readonly ConnectionIO _connectionIo;

		private readonly ObjectPool<TaskSlim> _taskLightPool;
		private readonly ObjectPool<FrameParameters.BasicPublishArgs> _basicPubArgsPool;

		public ChannelIO(Channel channel, ushort channelNumber, ConnectionIO connectionIo)
		{
			_channel = channel;
			_channelNum = channelNumber;
			_connectionIo = connectionIo;

			_taskLightPool = new ObjectPool<TaskSlim>(
				() => new TaskSlim(i => _channel.GenericRecycler(i, _taskLightPool)), 10, preInitialize: true);

			_basicPubArgsPool = new ObjectPool<FrameParameters.BasicPublishArgs>(
				() => new FrameParameters.BasicPublishArgs(i => _channel.GenericRecycler(i, _basicPubArgsPool)), 1000, preInitialize: true); 
		}

		public ushort ChannelNumber { get { return _channelNum; } }

		#region AmqpIOBase overrides

		internal override async Task HandleFrame(int classMethodId)
		{
			switch (classMethodId)
			{
				case AmqpClassMethodChannelLevelConstants.ChannelClose:
					_connectionIo._frameReader.Read_Channel_Close2(base.HandleCloseMethodFromServer);
					break;

				case AmqpClassMethodChannelLevelConstants.BasicDeliver:
					await _connectionIo._frameReader.Read_BasicDelivery(_channel.DispatchDeliveredMessage, _channel.RentBasicProperties());
					break;

				case AmqpClassMethodChannelLevelConstants.BasicReturn:
					await _connectionIo._frameReader.Read_BasicReturn(_channel.DispatchBasicReturn, _channel.RentBasicProperties());
					break;

//				// Basic Ack and NAck will be sent by the server if we enabled confirmation for this channel
				case AmqpClassMethodChannelLevelConstants.BasicAck:
					_connectionIo._frameReader.Read_BasicAck(_channel.ProcessAcks);
					break;

				case AmqpClassMethodChannelLevelConstants.BasicNAck:
					_connectionIo._frameReader.Read_BasicNAck(_channel.ProcessNAcks);
					break;

				case AmqpClassMethodChannelLevelConstants.ChannelFlow:
					_connectionIo._frameReader.Read_ChannelFlow(_channel.HandleChannelFlow);
					break;

				default:
					await base.HandReplyToAwaitingQueue(classMethodId);
					break;
			}
		}

		internal override Task SendCloseConfirmation()
		{
			return __SendChannelCloseOk();
		}

		internal override Task SendStartClose()
		{
			return __SendChannelClose(AmqpConstants.ReplySuccess, "bye");
		}

		protected override void InternalDispose()
		{
			
		}

		#endregion

		internal Task Open()
		{
			var tcs = new TaskCompletionSource<bool>();

			var writer = AmqpChannelLevelFrameWriter.ChannelOpen();

			_connectionIo.SendCommand(_channelNum, 20, 10, writer,
				reply: (channel, classMethodId, error) =>
				{
					if (classMethodId == AmqpClassMethodChannelLevelConstants.ChannelOpenOk)
					{
						_connectionIo._frameReader.Read_ChannelOpenOk((reserved) =>
						{
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

		#region Commands writing methods

		private Task __SendChannelClose(ushort replyCode, string message)
		{
			var tcs = new TaskCompletionSource<bool>();

			_connectionIo.SendCommand(_channelNum, 20, 40, AmqpChannelLevelFrameWriter.ChannelClose,
				reply: (channel, classMethodId, error) =>
				{
					if (classMethodId == AmqpClassMethodChannelLevelConstants.ChannelCloseOk)
					{
						// _connection._frameReader.Read_Channel_CloseOk(() =>
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

		private Task __SendChannelCloseOk()
		{
			var tcs = new TaskCompletionSource<bool>();

			_connectionIo.SendCommand(_channelNum, 20, 41, 
				AmqpChannelLevelFrameWriter.ChannelCloseOk, 
				reply: null, expectsReply: false, tcs: tcs);

			return tcs.Task;
		}

		internal Task __SendConfirmSelect(bool noWait)
		{
			var tcs = new TaskCompletionSource<bool>();

			_connectionIo.SendCommand(_channelNum, 85, 10,
				AmqpChannelLevelFrameWriter.ConfirmSelect(noWait),
				reply: (channel, classMethodId, error) =>
				{
					if (classMethodId == AmqpClassMethodChannelLevelConstants.ConfirmSelectOk)
					{
						tcs.SetResult(true);
					}
					else
					{
						AmqpIOBase.SetException(tcs, error, classMethodId);
					}
					return Task.CompletedTask;
				},
				expectsReply: true,
				tcs: tcs);

			return tcs.Task;
		}

		internal Task __BasicQos(uint prefetchSize, ushort prefetchCount, bool global)
		{
			var tcs = new TaskCompletionSource<bool>();

			var writer = AmqpChannelLevelFrameWriter.BasicQos(prefetchSize, prefetchCount, global);

			_connectionIo.SendCommand(_channelNum, 60, 10, writer,
				reply: (channel, classMethodId, error) =>
				{
					if (classMethodId == AmqpClassMethodChannelLevelConstants.BasicQosOk)
					{
						tcs.SetResult(true);
					}
					else
					{
						AmqpIOBase.SetException(tcs, error, classMethodId);
					}
					return Task.CompletedTask;
				}, expectsReply: true);

			return tcs.Task;
		}

		internal Task __BasicAck(ulong deliveryTag, bool multiple)
		{
			var tcs = new TaskCompletionSource<bool>();

			var args = new FrameParameters.BasicAckArgs() { deliveryTag = deliveryTag, multiple = multiple };

			_connectionIo.SendCommand(_channelNum, 60, 80,
				null, // writer
				reply: null,
				expectsReply: false,
				tcs: tcs,
				optArg: args);

			return tcs.Task;
		}

		internal Task __BasicNAck(ulong deliveryTag, bool multiple, bool requeue)
		{
			var tcs = new TaskCompletionSource<bool>();

			var args = new FrameParameters.BasicNAckArgs() { deliveryTag = deliveryTag, multiple = multiple, requeue = requeue };

			_connectionIo.SendCommand(_channelNum, 60, 120,
				null, // writer
				reply: null,
				expectsReply: false,
				tcs: tcs,
				optArg: args);

			return tcs.Task;
		}

		internal Task __ExchangeDeclare(string exchange, string type, bool durable, bool autoDelete,
			IDictionary<string, object> arguments, bool waitConfirmation)
		{
			var tcs = new TaskCompletionSource<bool>();

			var writer = AmqpChannelLevelFrameWriter.ExchangeDeclare(exchange, type, durable, autoDelete,
				arguments, false, false, waitConfirmation);

			_connectionIo.SendCommand(_channelNum, 40, 10, writer,
				reply: (channel, classMethodId, error) =>
				{
					if (!waitConfirmation || classMethodId == AmqpClassMethodChannelLevelConstants.ExchangeDeclareOk)
					{
						tcs.SetResult(true);
					}
					else
					{
						AmqpIOBase.SetException(tcs, error, classMethodId);
					}
					return Task.CompletedTask;
				}, expectsReply: waitConfirmation);

			return tcs.Task;
		}

		internal Task<AmqpQueueInfo> __QueueDeclare(string queue, bool passive, bool durable, bool exclusive, bool autoDelete,
			IDictionary<string, object> arguments, bool waitConfirmation)
		{
			var tcs = new TaskCompletionSource<AmqpQueueInfo>();

			var writer = AmqpChannelLevelFrameWriter.QueueDeclare(queue, passive, durable,
				exclusive, autoDelete, arguments, waitConfirmation);

			_connectionIo.SendCommand(_channelNum, 50, 10, writer,
				reply: async (channel, classMethodId, error) =>
				{
					if (waitConfirmation && classMethodId == AmqpClassMethodChannelLevelConstants.QueueDeclareOk)
					{
						await _connectionIo._frameReader.Read_QueueDeclareOk((queueName, messageCount, consumerCount) =>
						{
							tcs.SetResult(new AmqpQueueInfo()
							{
								Name = queueName,
								Consumers = consumerCount,
								Messages = messageCount
							});

							return Task.CompletedTask;
						});
					}
					else if (!waitConfirmation)
					{
						tcs.SetResult(new AmqpQueueInfo { Name = queue });
					}
					else
					{
						AmqpIOBase.SetException(tcs, error, classMethodId);
					}
				}, expectsReply: waitConfirmation);

			return tcs.Task;
		}

		internal Task __QueueBind(string queue, string exchange, string routingKey, IDictionary<string, object> arguments,
			bool waitConfirmation)
		{
			var tcs = new TaskCompletionSource<bool>();

			var writer = AmqpChannelLevelFrameWriter.QueueBind(queue, exchange, routingKey, arguments, waitConfirmation);

			_connectionIo.SendCommand(_channelNum, 50, 20, writer,
				reply: (channel, classMethodId, error) =>
				{
					if (!waitConfirmation || (classMethodId == AmqpClassMethodChannelLevelConstants.QueueBindOk))
					{
						tcs.SetResult(true);
					}
					else
					{
						AmqpIOBase.SetException(tcs, error, classMethodId);
					}
					return Task.CompletedTask;
				}, expectsReply: waitConfirmation);

			return tcs.Task;
		}

		public Task<string> __BasicConsume(ConsumeMode mode, string queue, string consumerTag, bool withoutAcks, 
										   bool exclusive, IDictionary<string, object> arguments, bool waitConfirmation, Action<string> confirmConsumerTag)
		{
			var tcs = new TaskCompletionSource<string>(
				mode == ConsumeMode.ParallelWithBufferCopy || mode == ConsumeMode.ParallelWithBufferCopy ?
				TaskCreationOptions.RunContinuationsAsynchronously : TaskCreationOptions.None);

			var writer = AmqpChannelLevelFrameWriter.BasicConsume(
				queue, consumerTag, withoutAcks, exclusive, arguments, waitConfirmation);

			_connectionIo.SendCommand(_channelNum, 60, 20, writer,
				reply: (channel, classMethodId, error) =>
				{
					if (waitConfirmation && classMethodId == AmqpClassMethodChannelLevelConstants.BasicConsumeOk)
					{
						_connectionIo._frameReader.Read_BasicConsumeOk((consumerTag2) =>
						{
							if (string.IsNullOrEmpty(consumerTag))
							{
								confirmConsumerTag(consumerTag2);
							}

							tcs.SetResult(consumerTag2);
						});
					}
					else if (!waitConfirmation)
					{
						tcs.SetResult(consumerTag);
					}
					else
					{
						AmqpIOBase.SetException(tcs, error, classMethodId);
					}

					return Task.CompletedTask;

				}, expectsReply: waitConfirmation);

			return tcs.Task;
		}

		public TaskSlim __BasicPublish(string exchange, string routingKey, bool mandatory, bool immediate,
										BasicProperties properties, ArraySegment<byte> buffer, 
										bool withTcs)
		{
			// TODO: log if the user withTcs=false and _confirmationKeeper != null which doesnt make sense...

			if (properties == null || properties.IsEmpty)
			{
				properties = BasicProperties.Empty;
			}

			var confirmationKeeper = _channel._confirmationKeeper;

			var needsHardConfirmation = confirmationKeeper != null;

			Func<ushort, int, AmqpError, Task> replyFunc = null;
			Action prepare = null;
			TaskSlim tcs = null;
			if (withTcs || needsHardConfirmation)
			{
				tcs = _taskLightPool.GetObject();
			}

//			if (properties.IsReusable)
//			{
//				replyFunc = (_c, _i, _error) => _channel.Return(properties);
//			}

			if (needsHardConfirmation) // we're in pub confirmation mode
			{
				confirmationKeeper.WaitForSemaphore(); // make sure we're not over the limit
				prepare = () => confirmationKeeper.Add(tcs);
			}

			var args = _basicPubArgsPool.GetObject();
			// var args = new FrameParameters.BasicPublishArgs(null);
			args.exchange = exchange;
			args.immediate = immediate;
			args.routingKey = routingKey;
			args.mandatory = mandatory;
			args.properties = properties;
			args.buffer = buffer;

			_connectionIo.SendCommand(_channelNum, 60, 40,
				null, // AmqpChannelLevelFrameWriter.InternalBasicPublish, 
				reply: replyFunc,
				expectsReply: false,
				tcsL: needsHardConfirmation ? null : tcs, // if needsHardConfirmation the tcs will be signaled by the confirmationKeeper
				optArg: args,
				prepare: prepare);

			return tcs;
		}

		public Task __BasicCancel(string consumerTag, bool waitConfirmation)
		{
			var tcs = new TaskCompletionSource<bool>();

			_connectionIo.SendCommand(_channelNum, 60, 30,
				AmqpChannelLevelFrameWriter.BasicCancel(consumerTag, waitConfirmation),
				reply: (channel, classMethodId, error) =>
				{
					if (waitConfirmation && classMethodId == AmqpClassMethodChannelLevelConstants.CancelOk)
					{
						_connectionIo._frameReader.Read_CancelOk((_) =>
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
						AmqpIOBase.SetException(tcs, error, classMethodId);
					}

					return Task.CompletedTask;
				}, expectsReply: waitConfirmation);

			return tcs.Task;
		}

		public Task __BasicRecover(bool requeue)
		{
			var tcs = new TaskCompletionSource<bool>();

			_connectionIo.SendCommand(_channelNum, 60, 110,
				AmqpChannelLevelFrameWriter.Recover(requeue),
				reply: (channel, classMethodId, error) =>
				{
					if (classMethodId == AmqpClassMethodChannelLevelConstants.RecoverOk)
					{
						tcs.SetResult(true);
					}
					else
					{
						AmqpIOBase.SetException(tcs, error, classMethodId);
					}
					return Task.CompletedTask;
				},
				expectsReply: true,
				tcs: tcs);

			return tcs.Task;
		}

		#endregion
	}
}