namespace RabbitMqNext
{
	using System;
	using System.Collections.Generic;
	using System.Threading.Tasks;
	using Internals;

	public class AutoRecoveryEnabledChannel : IChannel
	{
		const string LogSource = "ChannelRecovery";
		
		private readonly Channel _channel;

		public AutoRecoveryEnabledChannel(Channel channel)
		{
			_channel = channel;
		}

		#region Implementation of IChannel

		public event Action<AmqpError> OnError
		{
			add { _channel.OnError += value; }
			remove { _channel.OnError -= value; }
		}
		
		public Func<UndeliveredMessage, Task> MessageUndeliveredHandler 
		{ 
			get { return _channel.MessageUndeliveredHandler; } 
			set { _channel.MessageUndeliveredHandler = value; } 
		}
		
		public bool IsConfirmationEnabled
		{
			get { return _channel.IsConfirmationEnabled; }
		}
		
		public ushort ChannelNumber
		{
			get { return _channel.ChannelNumber; }
		}

		public bool IsClosed
		{
			get { return _channel.IsClosed; }
		}

		public BasicProperties RentBasicProperties()
		{
			return _channel.RentBasicProperties();
		}

		public void Return(BasicProperties properties)
		{
			_channel.Return(properties);
		}

		public Task BasicQos(uint prefetchSize, ushort prefetchCount, bool global)
		{
			return null;
		}

		public void BasicAck(ulong deliveryTag, bool multiple)
		{
		}

		public void BasicNAck(ulong deliveryTag, bool multiple, bool requeue)
		{
		}

		public Task ExchangeDeclare(string exchange, string type, bool durable, bool autoDelete, IDictionary<string, object> arguments,
			bool waitConfirmation)
		{
			return null;
		}

		public Task ExchangeBind(string source, string destination, string routingKey, IDictionary<string, object> arguments, bool waitConfirmation)
		{
			return null;
		}

		public Task ExchangeUnbind(string source, string destination, string routingKey, IDictionary<string, object> arguments, bool waitConfirmation)
		{
			return null;
		}

		public Task ExchangeDelete(string exchange, IDictionary<string, object> arguments, bool waitConfirmation)
		{
			return null;
		}

		public Task<AmqpQueueInfo> QueueDeclare(string queue, bool passive, bool durable, bool exclusive, bool autoDelete, IDictionary<string, object> arguments,
			bool waitConfirmation)
		{
			return null;
		}

		public Task QueueBind(string queue, string exchange, string routingKey, IDictionary<string, object> arguments, bool waitConfirmation)
		{
			return null;
		}

		public Task QueueUnbind(string queue, string exchange, string routingKey, IDictionary<string, object> arguments)
		{
			return null;
		}

		public Task QueueDelete(string queue, bool waitConfirmation)
		{
			return null;
		}

		public Task QueuePurge(string queue, bool waitConfirmation)
		{
			return null;
		}

		public TaskSlim BasicPublishWithConfirmation(string exchange, string routingKey, bool mandatory, BasicProperties properties,
			ArraySegment<byte> buffer)
		{
			return null;
		}

		public TaskSlim BasicPublish(string exchange, string routingKey, bool mandatory, BasicProperties properties,
			ArraySegment<byte> buffer)
		{
			return null;
		}

		public void BasicPublishFast(string exchange, string routingKey, bool mandatory, BasicProperties properties,
			ArraySegment<byte> buffer)
		{
		}

		public Task<string> BasicConsume(ConsumeMode mode, QueueConsumer consumer, string queue, string consumerTag, bool withoutAcks,
			bool exclusive, IDictionary<string, object> arguments, bool waitConfirmation)
		{
			return null;
		}

		public Task<string> BasicConsume(ConsumeMode mode, Func<MessageDelivery, Task> consumer, string queue, string consumerTag, bool withoutAcks, bool exclusive,
			IDictionary<string, object> arguments, bool waitConfirmation)
		{
			return null;
		}

		public Task BasicCancel(string consumerTag, bool waitConfirmation)
		{
			return null;
		}

		public Task BasicRecover(bool requeue)
		{
			return null;
		}

		public Task<RpcHelper> CreateRpcHelper(ConsumeMode mode, int? timeoutInMs, int maxConcurrentCalls = 500)
		{
			return null;
		}

		public Task Close()
		{
			return null;
		}

		#endregion

		#region Implementation of IDisposable

		public void Dispose()
		{
		}

		#endregion
	}
}