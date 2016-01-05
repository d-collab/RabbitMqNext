namespace RabbitMqNext
{
	using System;
	using System.Collections.Concurrent;
	using System.Collections.Generic;
	using System.IO;
	using System.Threading.Tasks;
	using Internals;

	internal class AmqpChannel : IAmqpChannel
	{
		private readonly ushort _channelNum;
		private readonly ConnectionStateMachine _connection;
		private readonly ConcurrentDictionary<string, Action<BasicProperties, Stream, int>> _consumerSubscriptions;
		internal readonly ConcurrentQueue<CommandToSend> _awaitingReplyQueue;

		private volatile int _closed = 0;

		private readonly ObjectPool<TaskLight> _taskLightPool;
		private readonly ObjectPool<FrameParameters.BasicPublishArgs> _basicPubArgsPool;

		public AmqpChannel(ushort channelNum, ConnectionStateMachine connection)
		{
			_channelNum = channelNum;
			_connection = connection;
			_awaitingReplyQueue = new ConcurrentQueue<CommandToSend>();
			_consumerSubscriptions = new ConcurrentDictionary<string, Action<BasicProperties, Stream, int>>(StringComparer.Ordinal);

			_taskLightPool = new ObjectPool<TaskLight>(() =>
				new TaskLight(i => GenericRecycler(i, _taskLightPool)), 10, preInitialize: true);
			
			_basicPubArgsPool = new ObjectPool<FrameParameters.BasicPublishArgs>(
				() => new FrameParameters.BasicPublishArgs(i => GenericRecycler(i, _basicPubArgsPool)), 10, preInitialize: true); 
		}

		private void GenericRecycler<T>(T item, ObjectPool<T> pool) where T : class
		{
			pool.PutObject(item);
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
				reply: (channel, classMethodId, error) =>
				{
					if (waitConfirmation && classMethodId == AmqpClassMethodChannelLevelConstants.QueueDeclareOk)
					{
						_connection._frameReader.Read_QueueDeclareOk((queueName, messageCount, consumerCount) =>
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

		public TaskLight BasicPublish(string exchange, string routingKey, bool mandatory, bool immediate, BasicProperties properties, ArraySegment<byte> buffer)
		{
			if (properties == null || properties.IsEmpty)
			{
				properties = BasicProperties.Empty;
			}

			// var tcs = new TaskCompletionSource<bool>();
			// var tcs = new TaskLight(null);
			var tcs = _taskLightPool.GetObject();

			var args = _basicPubArgsPool.GetObject();
			args.exchange = exchange; 
			args.immediate = immediate; 
			args.routingKey = routingKey; 
			args.mandatory = mandatory; 
			args.properties = properties;
			args.buffer = buffer;

			_connection.SendCommand(_channelNum, 60, 40,
				AmqpChannelLevelFrameWriter.InternalBasicPublish, reply: null, expectsReply: false, 
				// tcs: tcs,
				tcsL: tcs,
				optArg: args);

			return tcs;
			// return tcs.Task;
		}

		public Task<string> BasicConsume(Action<BasicProperties, Stream, int> consumer,
			string queue, string consumerTag, bool withoutAcks, bool exclusive, 
			IDictionary<string, object> arguments, bool waitConfirmation)
		{
			_consumerSubscriptions[consumerTag] = consumer;

			var tcs = new TaskCompletionSource<string>();

			var writer = AmqpChannelLevelFrameWriter.BasicConsume(queue, consumerTag, withoutAcks, exclusive, arguments, waitConfirmation);

			_connection.SendCommand(_channelNum, 60, 20, writer, 
				reply: (channel, classMethodId, error) =>
				{
					if (waitConfirmation && classMethodId == AmqpClassMethodChannelLevelConstants.BasicConsumeOk)
					{
						_connection._frameReader.Read_BasicConsumeOk((consumerTag2) =>
						{
							tcs.SetResult(consumerTag2);
						});
					}
					else if (!waitConfirmation)
					{
						tcs.SetResult(consumerTag);
					}
					else
					{
						Util.SetException(tcs, error, classMethodId);
					}
				}, expectsReply: waitConfirmation);

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
						// _connection._frameReader.Read_Channel_CloseOk(() =>
						{
							tcs.SetResult(true);
						}//);
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

		internal async Task DispatchMethod(ushort channel, int classMethodId)
		{
			switch (classMethodId)
			{
				case AmqpClassMethodChannelLevelConstants.BasicDeliver:

					await _connection._frameReader.Read_BasicDelivery(DispatchDeliveredMessage);
					break;

				default:
					Console.WriteLine("Unexpected method at channel level " + classMethodId);
					break;
			}
		}

		private void DispatchDeliveredMessage(string consumerTag, ulong deliveryTag, bool redelivered, string exchange, string routingKey,
			long bodySize, BasicProperties properties, Stream stream)
		{
			Action<BasicProperties, Stream, int> consumer;
			if (_consumerSubscriptions.TryGetValue(consumerTag, out consumer))
			{
				consumer(properties, stream, (int)bodySize);
			}
			else
			{
				// needs to consume the stream by exactly bodySize
				stream.Seek(bodySize, SeekOrigin.Current);
			}
		}
	}
}