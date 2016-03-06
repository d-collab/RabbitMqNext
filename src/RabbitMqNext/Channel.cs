namespace RabbitMqNext
{
	using System;
	using System.Collections.Concurrent;
	using System.Collections.Generic;
	using System.IO;
	using System.Threading;
	using System.Threading.Tasks;
	using RabbitMqNext.Internals;
	using RabbitMqNext.Internals.RingBuffer;
	using RabbitMqNext.Io;

	public class Channel : IChannel
	{
		private static readonly Stream EmptyStream = new MemoryStream(new byte[0], writable: false);

		private readonly CancellationToken _cancellationToken;

		internal readonly ChannelIO _io;
		internal MessagesPendingConfirmationKeeper _confirmationKeeper;

		private readonly ConcurrentDictionary<string, BasicConsumerSubscriptionInfo> _consumerSubscriptions;
		private readonly ObjectPool<BasicProperties> _propertiesPool;

		public Channel(ushort channelNumber, ConnectionIO connectionIo, CancellationToken cancellationToken)
		{
			_cancellationToken = cancellationToken;
			_io = new ChannelIO(this, channelNumber, connectionIo)
			{
				OnError = error =>
				{
					var ev = this.OnError;
					if (ev != null) ev(error);
				}
			};

			_consumerSubscriptions = new ConcurrentDictionary<string, BasicConsumerSubscriptionInfo>(StringComparer.Ordinal);

			_propertiesPool = new ObjectPool<BasicProperties>(() => new BasicProperties(false, reusable: true), 100, preInitialize: false);
		}

		public IChannelRecoveryStrategy ChannelRecoveryStrategy { get; internal set; }

		public event Action<AmqpError> OnError;

		public bool IsConfirmationEnabled
		{
			get { return _confirmationKeeper != null; }
		}

		public ushort ChannelNumber
		{
			get { return _io.ChannelNumber; }
		}

		public bool IsClosed { get { return _io.IsClosed; } }

		public Func<UndeliveredMessage, Task> MessageUndeliveredHandler { get; set; }

		public BasicProperties RentBasicProperties()
		{
			return _propertiesPool.GetObject();
		}

		public void Return(BasicProperties properties)
		{
			if (properties == null) throw new ArgumentNullException("properties");
			if (!properties.IsReusable) return;

			_propertiesPool.PutObject(properties);
		}

		public Task BasicQos(uint prefetchSize, ushort prefetchCount, bool global)
		{
			return _io.__BasicQos(prefetchSize, prefetchCount, global);
		}

		public void BasicAck(ulong deliveryTag, bool multiple)
		{
			_io.__BasicAck(deliveryTag, multiple);
		}

		public void BasicNAck(ulong deliveryTag, bool multiple, bool requeue)
		{
			_io.__BasicNAck(deliveryTag, multiple, requeue);
		}

		public Task ExchangeDeclare(string exchange, string type, bool durable, bool autoDelete,
			IDictionary<string, object> arguments, bool waitConfirmation)
		{
			return _io.__ExchangeDeclare(exchange, type, durable, autoDelete, arguments, waitConfirmation);
		}

		public Task ExchangeBind(string source, string destination, string routingKey, 
			IDictionary<string, object> arguments, bool waitConfirmation)
		{
			return _io.__ExchangeBind(source, destination, routingKey, arguments, waitConfirmation);
		}

		public Task ExchangeUnbind(string source, string destination, string routingKey,
			IDictionary<string, object> arguments, bool waitConfirmation)
		{
			return _io.__ExchangeUnbind(source, destination, routingKey, arguments, waitConfirmation);
		}

		public Task ExchangeDelete(string exchange, IDictionary<string, object> arguments, bool waitConfirmation)
		{
			return _io.__ExchangeDelete(exchange, arguments, waitConfirmation);
		}

		public Task<AmqpQueueInfo> QueueDeclare(string queue, bool passive, bool durable, bool exclusive, bool autoDelete,
			IDictionary<string, object> arguments, bool waitConfirmation)
		{
			return _io.__QueueDeclare(queue, passive, durable, exclusive, autoDelete, arguments, waitConfirmation);
		}

		public Task QueueBind(string queue, string exchange, string routingKey, IDictionary<string, object> arguments,
			bool waitConfirmation)
		{
			return _io.__QueueBind(queue, exchange, routingKey, arguments, waitConfirmation);
		}

		public Task QueueUnbind(string queue, string exchange, string routingKey, IDictionary<string, object> arguments)
		{
			return _io.__QueueUnbind(queue, exchange, routingKey, arguments);
		}

		public Task QueueDelete(string queue /*, bool ifUnused, bool ifEmpty*/, bool waitConfirmation)
		{
			return _io.__QueueDelete(queue, waitConfirmation);
		}

		public Task QueuePurge(string queue, bool waitConfirmation)
		{
			return _io.__QueuePurge(queue, waitConfirmation);
		}

		public TaskSlim BasicPublishWithConfirmation(string exchange, string routingKey, bool mandatory,
			BasicProperties properties, ArraySegment<byte> buffer)
		{
			if (_confirmationKeeper == null) throw new Exception("This channel is not set up for confirmations");

			return _io.__BasicPublishConfirm(exchange, routingKey, mandatory, properties, buffer);
		}

		public TaskSlim BasicPublish(string exchange, string routingKey, bool mandatory, 
			BasicProperties properties, ArraySegment<byte> buffer)
		{
			if (_confirmationKeeper != null) throw new Exception("This channel is set up for confirmations, call BasicPublishWithConfirmation instead");

			return _io.__BasicPublishTask(exchange, routingKey, mandatory, properties, buffer);
		}

		public void BasicPublishFast(string exchange, string routingKey, bool mandatory, 
			BasicProperties properties, ArraySegment<byte> buffer)
		{
			if (_confirmationKeeper != null) throw new Exception("This channel is set up for confirmations, call BasicPublishWithConfirmation instead");

			_io.__BasicPublish(exchange, routingKey, mandatory, properties, buffer);
		}

		public Task<string> BasicConsume(ConsumeMode mode, QueueConsumer consumer,
			string queue, string consumerTag, bool withoutAcks, bool exclusive,
			IDictionary<string, object> arguments, bool waitConfirmation)
		{
			if (consumer == null) throw new ArgumentNullException("consumer");
			if (!waitConfirmation && string.IsNullOrEmpty(consumerTag))
				throw new ArgumentException("You must specify a consumer tag if waitConfirmation = false");

			if (!string.IsNullOrEmpty(consumerTag))
			{
				_consumerSubscriptions[consumerTag] = new BasicConsumerSubscriptionInfo
				{
					Mode = mode,
					_consumer = consumer
				};
			}

			return _io.__BasicConsume(mode, queue, consumerTag, withoutAcks, exclusive, arguments, waitConfirmation,
				consumerTag2 =>
				{
					_consumerSubscriptions[consumerTag2] = new BasicConsumerSubscriptionInfo
					{
						Mode = mode,
						_consumer = consumer
					};
				});
		}

		public Task<string> BasicConsume(ConsumeMode mode, Func<MessageDelivery, Task> consumer,
			string queue, string consumerTag, bool withoutAcks, bool exclusive,
			IDictionary<string, object> arguments, bool waitConfirmation)
		{
			if (consumer == null) throw new ArgumentNullException("consumer");
			if (!waitConfirmation && string.IsNullOrEmpty(consumerTag)) 
				throw new ArgumentException("You must specify a consumer tag if waitConfirmation = false");

			if (!string.IsNullOrEmpty(consumerTag))
			{
				_consumerSubscriptions[consumerTag] = new BasicConsumerSubscriptionInfo
				{
					Mode = mode,
					Callback = consumer
				};
			}

			return _io.__BasicConsume(mode, queue, consumerTag, withoutAcks, exclusive, arguments, waitConfirmation,
				consumerTag2 =>
				{
					_consumerSubscriptions[consumerTag2] = new BasicConsumerSubscriptionInfo
					{
						Mode = mode,
						Callback = consumer
					};
				});
		}

		public Task BasicCancel(string consumerTag, bool waitConfirmation)
		{
			return _io.__BasicCancel(consumerTag, waitConfirmation);
		}

		public Task BasicRecover(bool requeue)
		{
			return _io.__BasicRecover(requeue);
		}

		public Task<RpcHelper> CreateRpcHelper(ConsumeMode mode, int? timeoutInMs, int maxConcurrentCalls = 500)
		{
			if (_confirmationKeeper != null) throw new Exception("This channel is set up for confirmations");

			return RpcHelper.Create(this, maxConcurrentCalls, mode, timeoutInMs);
		}

		public async Task Close()
		{
			await this._io.InitiateCleanClose(false, null).ConfigureAwait(false);

			this.Dispose();
		}

		public void Dispose()
		{
			this._io.Dispose();
		}

		internal async Task DispatchDeliveredMessage(
			string consumerTag, ulong deliveryTag, bool redelivered,
			string exchange, string routingKey, int bodySize, 
			BasicProperties properties, RingBufferStreamAdapter ringBufferStream)
		{
			BasicConsumerSubscriptionInfo consumer;

			if (_consumerSubscriptions.TryGetValue(consumerTag, out consumer))
			{
				var delivery = new MessageDelivery
				{
					bodySize = bodySize,
					properties = properties,
					routingKey = routingKey,
					deliveryTag = deliveryTag,
					redelivered = redelivered
				};

				var mode = consumer.Mode;
				var cb = consumer.Callback;
				var consumerInstance = consumer._consumer;

				if (mode == ConsumeMode.SingleThreaded)
				{
					// run with scissors, we're letting 
					// the user code mess with the ring buffer in the name of performance
					delivery.stream = bodySize == 0 ? EmptyStream : ringBufferStream;

					// upon return it's assumed the user has consumed from the stream and is done with it
					var marker = new RingBufferPositionMarker(ringBufferStream._ringBuffer);

					try
					{
						if (cb != null)
							await cb(delivery).ConfigureAwait(false);
						else
							await consumerInstance.Consume(delivery).ConfigureAwait(false);
					}
					finally
					{
						// fingers crossed the user cloned the buffer if she needs it later
						this.Return(properties);

						var totalRead = marker.LengthRead;
						if (totalRead < bodySize)
						{
							checked
							{
								int offset = (int)(bodySize - totalRead);
								ringBufferStream.Seek(offset, SeekOrigin.Current);
							}
						}
					}
				}
				else 
				{
					// parallel mode. it cannot hold the frame handler, so we copy the buffer (yuck) and more forward

					if (mode == ConsumeMode.ParallelWithBufferCopy)
					{
						delivery.stream = delivery.bodySize == 0 ? 
							EmptyStream :
							new MemoryStream(BufferUtil.Copy(ringBufferStream, (int)bodySize), 0, bodySize, writable: false);
					}
//					else if (mode == ConsumeMode.ParallelWithReadBarrier)
//					{
//						// create reader barrier. once they are all done, 
//						// move the read pos forward. Shouldnt be too hard to implement and 
//						// avoids the new buffer + GC and keeps the api Stream based consistently
//
//						delivery.stream = delivery.bodySize == 0 ? 
//							EmptyStream : 
//							new RingBufferStreamReadBarrier(ringBufferStream, delivery.bodySize);
//
//						if (delivery.bodySize != 0)
//						{
//							var skipped = await ringBufferStream._ringBuffer.Skip(delivery.bodySize);
//							if (skipped != delivery.bodySize)
//							{
//								Console.Error.WriteLine("Skipped " + skipped + " but needed to skip " + delivery.bodySize);
//							}
//						}
//					}

					Task.Factory.StartNew(async () =>
					{
						try
						{
							if (cb != null)
								await cb(delivery).ConfigureAwait(false);
							else
								await consumerInstance.Consume(delivery).ConfigureAwait(false);
						}
						catch (Exception e)
						{
							LogAdapter.LogError("Channel", "Error processing message (user code)", e);
						}
						finally
						{
							this.Return(properties);

							if (delivery.bodySize != 0)
								delivery.stream.Dispose();
						}
					}, TaskCreationOptions.PreferFairness)
						.Unwrap()
						.IntentionallyNotAwaited();
				}
			}
			else
			{
				// received msg but nobody was subscribed to get it (?) TODO: log it at least
			}
		}

		internal async Task DispatchBasicReturn(ushort replyCode, string replyText, 
			string exchange, string routingKey, int bodySize, 
			BasicProperties properties, RingBufferStreamAdapter ringBufferStream)
		{
			var ev = this.MessageUndeliveredHandler;
			var marker = new RingBufferPositionMarker(ringBufferStream._ringBuffer);

			try
			{
				if (ev != null)
				{
					var inst = new UndeliveredMessage
					{
						bodySize = bodySize,
						stream = bodySize == 0 ? EmptyStream : ringBufferStream,
						properties = properties,
						routingKey = routingKey,
						replyCode = replyCode,
						replyText = replyText,
						exchange = exchange
					};

					await ev(inst).ConfigureAwait(false);
				}
			}
			finally
			{
				var totalRead = marker.LengthRead;
				if (totalRead < bodySize)
				{
					checked
					{
						int offset = (int)(bodySize - totalRead);
						ringBufferStream.Seek(offset, SeekOrigin.Current); // may block!
					}
				}
			}
		}

		internal void ProcessAcks(ulong deliveryTags, bool multiple)
		{
			if (_confirmationKeeper != null)
			{
				_confirmationKeeper.Confirm(deliveryTags, multiple, requeue: false, isAck: true);
			}
		}

		internal void ProcessNAcks(ulong deliveryTags, bool multiple, bool requeue)
		{
			if (_confirmationKeeper != null)
			{
				_confirmationKeeper.Confirm(deliveryTags, multiple, requeue, isAck: false);
			}
		}

		internal void HandleChannelFlow(bool isActive)
		{
			if (isActive)
			{

			}
			else
			{

			}
		}

		internal Task EnableConfirmation(int maxunconfirmedMessages)
		{
			if (_confirmationKeeper != null) throw new Exception("Already set");
		
			_confirmationKeeper = new MessagesPendingConfirmationKeeper(maxunconfirmedMessages, _cancellationToken);

			return _io.__SendConfirmSelect(noWait: false);
		}

		internal Task Open()
		{
			return _io.Open();
		}

		internal void GenericRecycler<T>(T item, ObjectPool<T> pool) where T : class
		{
			pool.PutObject(item);
		}

		class BasicConsumerSubscriptionInfo
		{
			public ConsumeMode Mode;
			public Func<MessageDelivery, Task> Callback;
			public QueueConsumer _consumer;
		}
	}
}
