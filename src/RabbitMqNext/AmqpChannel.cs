namespace RabbitMqNext
{
	using System;
	using System.Collections.Concurrent;
	using System.Collections.Generic;
	using System.IO;
	using System.Threading.Tasks;
	using Internals;

	public class AmqpChannel // : IAmqpChannel
	{
		private readonly ushort _channelNum;
		private readonly ConnectionStateMachine _connection;
		private readonly ConcurrentDictionary<string, Func<MessageDelivery, Task>> _consumerSubscriptions;
		internal readonly ConcurrentQueue<CommandToSend> _awaitingReplyQueue;

		private volatile int _closed = 0;

		private readonly ObjectPool<TaskLight> _taskLightPool;
		private readonly ObjectPool<FrameParameters.BasicPublishArgs> _basicPubArgsPool;

		internal AmqpChannel(ushort channelNum, ConnectionStateMachine connection)
		{
			_channelNum = channelNum;
			_connection = connection;
			_awaitingReplyQueue = new ConcurrentQueue<CommandToSend>();
			_consumerSubscriptions = new ConcurrentDictionary<string, Func<MessageDelivery, Task>>(StringComparer.Ordinal);

			_taskLightPool = new ObjectPool<TaskLight>(() =>
				new TaskLight(i => GenericRecycler(i, _taskLightPool)), 10, preInitialize: true);
			
			_basicPubArgsPool = new ObjectPool<FrameParameters.BasicPublishArgs>(
				() => new FrameParameters.BasicPublishArgs(i => GenericRecycler(i, _basicPubArgsPool)), 10, preInitialize: true); 
		}

		public Func<UndeliveredMessage, Task> MessageUndeliveredHandler;

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
						// _connection._frameReader.Read_BasicQosOk(() =>
						{
							tcs.SetResult(true);
						}// );
					}
					else
					{
						Util.SetException(tcs, error, classMethodId);
					}
					return Task.CompletedTask;
				}, expectsReply: true);

			return tcs.Task;
		}

		/// <summary>
		/// When sent by the client, this method acknowledges one or 
		/// more messages delivered via the Deliver or Get-Ok methods.
		/// </summary>
		/// <param name="deliveryTag">single message or a set of messages up to and including a specific message.</param>
		/// <param name="multiple">if true, up to and including a specific message</param>
		public Task BasicAck(ulong deliveryTag, bool multiple)
		{
			var tcs = new TaskCompletionSource<bool>();

			var args = new FrameParameters.BasicNAckArgs() { deliveryTag = deliveryTag, multiple = multiple };

			_connection.SendCommand(_channelNum, 60, 80,
				null, // writer
				reply: null,
				expectsReply: false,
				tcs: tcs,
				optArg: args);

			return tcs.Task;
		}

		/// <summary>
		/// This method allows a client to reject one or more incoming messages. 
		/// It can be used to interrupt and cancel large incoming messages, or 
		/// return untreatable messages to their original queue.
		/// </summary>
		/// <param name="deliveryTag">single message or a set of messages up to and including a specific message.</param>
		/// <param name="multiple">if true, up to and including a specific message</param>
		/// <param name="requeue">If requeue is true, the server will attempt to requeue the message.  
		/// If requeue is false or the requeue  attempt fails the messages are discarded or dead-lettered.</param>
		public Task BasicNAck(ulong deliveryTag, bool multiple, bool requeue)
		{
			var tcs = new TaskCompletionSource<bool>();

			var args = new FrameParameters.BasicNAckArgs() { deliveryTag = deliveryTag, multiple = multiple, requeue = requeue };

			_connection.SendCommand(_channelNum, 60, 120,
				null, // writer
				reply: null, 
				expectsReply: false,
				tcs: tcs,
				optArg: args);

			return tcs.Task;
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
						// _connection._frameReader.Read_ExchangeDeclareOk(() =>
						{
							tcs.SetResult(true);
						} // );
					}
					else if (!waitConfirmation)
					{
						tcs.SetResult(true);
					}
					else
					{
						Util.SetException(tcs, error, classMethodId);
					}
					return Task.CompletedTask;
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

							return Task.CompletedTask;
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
						// _connection._frameReader.Read_QueueBindOk(() =>
						{
							tcs.SetResult(true);
						}//);
					}
					else if (!waitConfirmation)
					{
						tcs.SetResult(true);
					}
					else
					{
						Util.SetException(tcs, error, classMethodId);
					}
					return Task.CompletedTask;
				}, expectsReply: waitConfirmation);

			return tcs.Task;
		}

		public TaskLight BasicPublish(string exchange, string routingKey, bool mandatory, bool immediate, BasicProperties properties, ArraySegment<byte> buffer)
		{
			if (properties == null || properties.IsEmpty)
			{
				properties = BasicProperties.Empty;
			}

			// var tcs = new TaskLight(null);
			var tcs = _taskLightPool.GetObject();

			var args = _basicPubArgsPool.GetObject();
			// var args = new FrameParameters.BasicPublishArgs(null);
			args.exchange = exchange; 
			args.immediate = immediate; 
			args.routingKey = routingKey; 
			args.mandatory = mandatory; 
			args.properties = properties;
			args.buffer = buffer;

			_connection.SendCommand(_channelNum, 60, 40,
				null, // AmqpChannelLevelFrameWriter.InternalBasicPublish, 
				reply: null, expectsReply: false, 
				// tcs: tcs,
				tcsL: tcs,
				optArg: args);

			return tcs;
		}

		public Task<string> BasicConsume(Func<MessageDelivery, Task> consumer,
			string queue, string consumerTag, bool withoutAcks, bool exclusive, 
			IDictionary<string, object> arguments, bool waitConfirmation)
		{
			_consumerSubscriptions[consumerTag] = consumer;

			var tcs = new TaskCompletionSource<string>();

			var writer = AmqpChannelLevelFrameWriter.BasicConsume(queue, consumerTag, withoutAcks, exclusive, arguments, waitConfirmation);

			_connection.SendCommand(_channelNum, 60, 20, writer, 
				reply: async (channel, classMethodId, error) =>
				{
					if (waitConfirmation && classMethodId == AmqpClassMethodChannelLevelConstants.BasicConsumeOk)
					{
						await _connection._frameReader.Read_BasicConsumeOk((consumerTag2) =>
						{
							tcs.SetResult(consumerTag2);

							return Task.CompletedTask;
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

		public Task BasicCancel(string consumerTag, bool waitConfirmation)
		{
			var tcs = new TaskCompletionSource<bool>();

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
				reply: (channel, classMethodId, error) =>
				{
					if (classMethodId == AmqpClassMethodChannelLevelConstants.ChannelOpenOk)
					{
						_connection._frameReader.Read_ChannelOpenOk((reserved) =>
						{
							tcs.SetResult(true);
						});
					}
					else
					{
						Util.SetException(tcs, error, classMethodId);
					}
					return Task.CompletedTask;
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

				case AmqpClassMethodChannelLevelConstants.BasicReturn:
					await _connection._frameReader.Read_BasicReturn(DispatchBasicReturn);
					break;

				default:
					Console.WriteLine("Unexpected method at channel level " + classMethodId);
					break;
			}
		}

		private async Task DispatchDeliveredMessage(string consumerTag, ulong deliveryTag, 
			bool redelivered, string exchange, string routingKey,
			long bodySize, BasicProperties properties, Stream stream)
		{
			Func<MessageDelivery, Task> consumer;
			if (_consumerSubscriptions.TryGetValue(consumerTag, out consumer))
			{
				var delivery = new MessageDelivery()
				{
					bodySize = bodySize, properties = properties, routingKey = routingKey, 
					stream = stream, deliveryTag = deliveryTag, redelivered = redelivered
				};

				await consumer(delivery);
			}
		}

		private async Task DispatchBasicReturn(ushort replyCode, 
									string replyText, string exchange, string routingKey, 
									uint bodySize, BasicProperties properties, Stream stream)
		{
			var ev = this.MessageUndeliveredHandler;

			// var consumed = 0;

			if (ev != null)
			{
				var inst = new UndeliveredMessage()
				{
					bodySize = bodySize,
					stream = stream,
					properties = properties,
					routingKey = routingKey,
					replyCode = replyCode,
					replyText = replyText,
					exchange = exchange
				};

				await ev(inst);
			}
		}

		private void GenericRecycler<T>(T item, ObjectPool<T> pool) where T : class
		{
			pool.PutObject(item);
		}
	}
}