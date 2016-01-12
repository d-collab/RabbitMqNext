namespace RabbitMqNext
{
	using System;
	using System.Collections.Concurrent;
	using System.Collections.Generic;
	using System.IO;
	using System.Threading.Tasks;
	using Internals;
	using Internals.RingBuffer;

	public class AmqpChannel // : IAmqpChannel
	{
		private readonly ushort _channelNum;
		private readonly ConnectionStateMachine _connection;
		private readonly ConcurrentDictionary<string, BasicConsumerSubscriptionInfo> _consumerSubscriptions;
		internal readonly ConcurrentQueue<CommandToSend> _awaitingReplyQueue;

		internal MessagesPendingConfirmationKeeper _confirmationKeeper;

		private volatile int _closed = 0;

		private readonly ObjectPool<TaskLight> _taskLightPool;
		private readonly ObjectPool<FrameParameters.BasicPublishArgs> _basicPubArgsPool;

		internal AmqpChannel(ushort channelNum, ConnectionStateMachine connection)
		{
			_channelNum = channelNum;
			_connection = connection;
			_awaitingReplyQueue = new ConcurrentQueue<CommandToSend>();
			_consumerSubscriptions = new ConcurrentDictionary<string, BasicConsumerSubscriptionInfo>(StringComparer.Ordinal);

			_taskLightPool = new ObjectPool<TaskLight>(() =>
				new TaskLight(i => GenericRecycler(i, _taskLightPool)), 10, preInitialize: true);
			
			_basicPubArgsPool = new ObjectPool<FrameParameters.BasicPublishArgs>(
				() => new FrameParameters.BasicPublishArgs(i => GenericRecycler(i, _basicPubArgsPool)), 1000, preInitialize: true); 
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

		public void BasicPublishN(string exchange, string routingKey, bool mandatory, bool immediate,
			BasicProperties properties, ArraySegment<byte> buffer)
		{
			InternalBasicPublish(exchange, routingKey, mandatory, immediate, properties, buffer, false);
		}

		public TaskLight BasicPublish(string exchange, string routingKey, bool mandatory, bool immediate, BasicProperties properties, ArraySegment<byte> buffer)
		{
			return InternalBasicPublish(exchange, routingKey, mandatory, immediate, properties, buffer, true);
		}

		public async Task<RpcHelper> CreateRpcHelper(ConsumeMode mode, int maxConcurrentCalls = 500)
		{
			var helper = new RpcHelper(this, maxConcurrentCalls, mode);
			await helper.Setup();
			return helper;
		}

		public Task<string> BasicConsume(ConsumeMode mode, Func<MessageDelivery, Task> consumer,
			string queue, string consumerTag, bool withoutAcks, bool exclusive, 
			IDictionary<string, object> arguments, bool waitConfirmation)
		{
			if (!waitConfirmation && string.IsNullOrEmpty(consumerTag)) throw new ArgumentException("Bad programmer");

			if (!string.IsNullOrEmpty(consumerTag))
			{
				_consumerSubscriptions[consumerTag] = new BasicConsumerSubscriptionInfo()
				{
					Mode = mode,
					Callback = consumer
				};
			}

			var tcs = new TaskCompletionSource<string>(
				mode == ConsumeMode.ParallelWithBufferCopy || mode == ConsumeMode.ParallelWithBufferCopy ? 
				TaskCreationOptions.RunContinuationsAsynchronously : TaskCreationOptions.None);

			var writer = AmqpChannelLevelFrameWriter.BasicConsume(
				queue, consumerTag, withoutAcks, exclusive, arguments, waitConfirmation);

			_connection.SendCommand(_channelNum, 60, 20, writer,
				reply: (channel, classMethodId, error) =>
				{
					if (waitConfirmation && classMethodId == AmqpClassMethodChannelLevelConstants.BasicConsumeOk)
					{
						_connection._frameReader.Read_BasicConsumeOk((consumerTag2) =>
						{
							if (string.IsNullOrEmpty(consumerTag))
							{
								_consumerSubscriptions[consumerTag2] = new BasicConsumerSubscriptionInfo()
								{
									Mode = mode,
									Callback = consumer
								};
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
						Util.SetException(tcs, error, classMethodId);
					}

					return Task.CompletedTask;

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

		internal Task __EnableConfirmation(int maxunconfirmedMessages)
		{
			if (_confirmationKeeper != null) throw new Exception("Already set");

			_confirmationKeeper = new MessagesPendingConfirmationKeeper(maxunconfirmedMessages, _connection._cancellationToken);

			var tcs = new TaskCompletionSource<bool>();

			_connection.SendCommand(_channelNum, 85, 10, AmqpChannelLevelFrameWriter.ConfirmSelect(noWait: false),
				reply: (channel, classMethodId, error) =>
				{
					if (classMethodId == AmqpClassMethodChannelLevelConstants.ConfirmSelectOk)
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
				tcs: tcs);

			return tcs.Task;
		}

		/// <summary>
		/// This method asks the server to redeliver all unacknowledged messages on a 
		/// specified channel. Zero or more messages may be redelivered.  This method
		/// </summary>
		/// <param name="requeue">  If this field is zero, the message will be redelivered to the original 
		/// recipient. If this bit is 1, the server will attempt to requeue the message, 
		/// potentially then delivering it to an alternative subscriber.</param>
		/// <returns></returns>
		public Task BasicRecover(bool requeue)
		{
			var tcs = new TaskCompletionSource<bool>();

			_connection.SendCommand(_channelNum, 60, 110,
				AmqpChannelLevelFrameWriter.Recover(requeue),
				reply: (channel, classMethodId, error) =>
				{
					if (classMethodId == AmqpClassMethodChannelLevelConstants.RecoverOk)
					{
						tcs.SetResult(true);
					}
					else
					{
						Util.SetException(tcs, error, classMethodId);
					}
					return Task.CompletedTask;
				},
				expectsReply: true,
				tcs: tcs);

			return tcs.Task;
		}

		private TaskLight InternalBasicPublish(string exchange, string routingKey, bool mandatory, bool immediate,
			BasicProperties properties, ArraySegment<byte> buffer, bool withTcs)
		{
			// TODO: log if the user withTcs=false and _confirmationKeeper != null which doesnt make sense...

			if (properties == null || properties.IsEmpty)
			{
				properties = BasicProperties.Empty;
			}

			var needsHardConfirmation = _confirmationKeeper != null;

			TaskLight tcs = null;
			if (withTcs || needsHardConfirmation)
			{
				tcs = _taskLightPool.GetObject();
			}

			if (needsHardConfirmation) // we're in pub confirmation mode
			{
				_confirmationKeeper.WaitForSemaphore(); // make sure we're not over the limit
			}

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
				reply: null, 
				expectsReply: false,
				// tcs: tcs,
				tcsL: needsHardConfirmation ? null : tcs,
				optArg: args,
				prepare: () => { _confirmationKeeper.Add(tcs); });

			return tcs;
		}

		private async Task InternalClose(bool sendClose)
		{
			if (sendClose)
				await __SendChannelClose(AmqpConstants.ReplySuccess, "bye");
			else
				await __SendChannelCloseOk();

			_connection.ChannelClosed(this);
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

				// Basic Ack and NAck will be sent by the server if we enabled confirmation for this channel
				case AmqpClassMethodChannelLevelConstants.BasicAck:
					_connection._frameReader.Read_BasicAck(ProcessAcks);
					break;
				
				case AmqpClassMethodChannelLevelConstants.BasicNAck:
					_connection._frameReader.Read_BasicNAck(ProcessNAcks);
					break;

				case AmqpClassMethodChannelLevelConstants.ChannelFlow:
					_connection._frameReader.Read_ChannelFlow(HandleChannelFlow);
					break;

				default:
					Console.WriteLine("Unexpected method at channel level " + classMethodId);
					break;
			}
		}

		private void HandleChannelFlow(bool isActive)
		{
			if (isActive)
			{

			}
			else
			{
				
			}
		}

		private void ProcessAcks(ulong deliveryTags, bool multiple)
		{
			if (_confirmationKeeper != null)
			{
				_confirmationKeeper.Confirm(deliveryTags, multiple, requeue: false, isAck: true);
			}
		}

		private void ProcessNAcks(ulong deliveryTags, bool multiple, bool requeue)
		{
			if (_confirmationKeeper != null)
			{
				_confirmationKeeper.Confirm(deliveryTags, multiple, requeue, isAck: false);
			}
		}

		private Task DispatchDeliveredMessage(string consumerTag, ulong deliveryTag, 
			bool redelivered, string exchange, string routingKey,
			int bodySize, BasicProperties properties, Stream stream)
		{
			BasicConsumerSubscriptionInfo consumer;

			if (_consumerSubscriptions.TryGetValue(consumerTag, out consumer))
			{
				var delivery = new MessageDelivery()
				{
					bodySize = bodySize,
					properties = properties,
					routingKey = routingKey,
					deliveryTag = deliveryTag,
					redelivered = redelivered
				};

				var mode = consumer.Mode;
				var cb = consumer.Callback;

				if (mode == ConsumeMode.SingleThreaded)
				{
					// run with scissors
					delivery.stream = stream;

					// upon return it's assumed the user has consumed from the stream and is done with it
					return cb(delivery); 
				}
				// else if (mode == ConsumeMode.ParallelWithBufferCopy)
				{
					// parallel mode. it cannot hold the frame handler, so we copy the buffer (yuck) and more forward

					// since we dont have any control on how the user 
					// will deal with the buffer we cant even re-use/use a pool, etc :-(

					// Idea: split Ringbuffer consumers, create reader barrier. once they are all done, 
					// move the read pos forward. Shouldnt be too hard to implement and 
					// avoids the new buffer + GC and keeps the api Stream based consistently

					if (mode == ConsumeMode.ParallelWithBufferCopy)
					{
						var bufferCopy = BufferUtil.Copy(stream as RingBufferStreamAdapter, (int) bodySize);
						var memStream = new MemoryStream(bufferCopy, writable: false );
						delivery.stream = memStream;
					}
					else if (mode == ConsumeMode.ParallelWithReadBarrier)
					{
						var readBarrier = new RingBufferStreamReadBarrier(stream as RingBufferStreamAdapter, delivery.bodySize);
						delivery.stream = readBarrier;
					}

					Task.Factory.StartNew(() => {
						try
						{
							using (delivery.stream)
							{
								cb(delivery);
							}
						}
						catch (Exception e)
						{
							Console.WriteLine("From threadpool " + e);
						}
					}, TaskCreationOptions.PreferFairness);

					// Fingers crossed the threadpool is large enough
//					ThreadPool.UnsafeQueueUserWorkItem((param) =>
//					{
//						try
//						{
//							using (delivery.stream)
//							{
//								cb(delivery);
//							}
//						}
//						catch (Exception e)
//						{
//							Console.WriteLine("From threadpool " + e);
//						}
//					}, null);

					return Task.CompletedTask;
				}
			}
			else
			{
				// received msg but nobody was subscribed to get it (?) TODO: log it at least
			}

			return Task.CompletedTask;
		}

		private async Task DispatchBasicReturn(ushort replyCode, 
									string replyText, string exchange, string routingKey, 
									int bodySize, BasicProperties properties, Stream stream)
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

		class BasicConsumerSubscriptionInfo
		{
			public ConsumeMode Mode;
			public Func<MessageDelivery, Task> Callback;
		}
	}
}