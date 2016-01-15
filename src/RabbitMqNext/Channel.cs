namespace RabbitMqNext
{
	using System;
	using System.Collections.Concurrent;
	using System.Collections.Generic;
	using System.Threading;
	using System.Threading.Tasks;
	using Internals;

	public class Channel : IDisposable
	{
		private readonly CancellationToken _cancellationToken;
		internal readonly ChannelIO _io;
		internal MessagesPendingConfirmationKeeper _confirmationKeeper;

		private readonly ConcurrentDictionary<string, BasicConsumerSubscriptionInfo> _consumerSubscriptions;

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
		}

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

		public Task BasicQos(uint prefetchSize, ushort prefetchCount, bool global)
		{
			return _io.__BasicQos(prefetchSize, prefetchCount, global);
		}

		public Task BasicAck(ulong deliveryTag, bool multiple)
		{
			return _io.__BasicAck(deliveryTag, multiple);
		}

		public Task BasicNAck(ulong deliveryTag, bool multiple, bool requeue)
		{
			return _io.__BasicNAck(deliveryTag, multiple, requeue);
		}

		public Task ExchangeDeclare(string exchange, string type, bool durable, bool autoDelete,
			IDictionary<string, object> arguments, bool waitConfirmation)
		{
			return _io.__ExchangeDeclare(exchange, type, durable, autoDelete, arguments, waitConfirmation);
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

		public void BasicPublishFast(string exchange, string routingKey, bool mandatory, bool immediate,
			BasicProperties properties, ArraySegment<byte> buffer)
		{
			_io.__BasicPublish(exchange, routingKey, mandatory, immediate, properties, buffer, false);
		}

		public TaskLight BasicPublish(string exchange, string routingKey, bool mandatory, bool immediate,
			BasicProperties properties, ArraySegment<byte> buffer)
		{
			return _io.__BasicPublish(exchange, routingKey, mandatory, immediate, properties, buffer, true);
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

//		public async Task<RpcHelper> CreateRpcHelper(ConsumeMode mode, int maxConcurrentCalls = 500)
//		{
//			var helper = new RpcHelper(this, maxConcurrentCalls, mode);
//			await helper.Setup();
//			return helper;
//		}

		public async Task Close()
		{
			await this._io.InitiateCleanClose(false, null);

			this.Dispose();
		}

		public void Dispose()
		{
			this._io.Dispose();
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
		}
	}
}
