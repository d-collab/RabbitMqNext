namespace RabbitMqNext
{
	using System;
	using System.Collections.Generic;
	using System.Threading.Tasks;
	using Internals;
	using Internals.RingBuffer;


	public class RecoveryEnabledChannel : IChannel
	{
		const string LogSource = "ChannelRecovery";
		
		private readonly Channel _channel;

		public RecoveryEnabledChannel(Channel channel)
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
			return _channel.BasicQos(prefetchSize, prefetchCount, global);
		}

		public void BasicAck(ulong deliveryTag, bool multiple)
		{
			_channel.BasicAck(deliveryTag, multiple);
		}

		public void BasicNAck(ulong deliveryTag, bool multiple, bool requeue)
		{
			_channel.BasicNAck(deliveryTag, multiple, requeue);
		}

		public Task ExchangeDeclare(string exchange, string type, bool durable, bool autoDelete, IDictionary<string, object> arguments,
			bool waitConfirmation)
		{
			return _channel.ExchangeDeclare(exchange, type, durable, autoDelete, arguments, waitConfirmation);
		}

		public Task ExchangeBind(string source, string destination, string routingKey, IDictionary<string, object> arguments, bool waitConfirmation)
		{
			return _channel.ExchangeBind(source, destination, routingKey, arguments, waitConfirmation);
		}

		public Task ExchangeUnbind(string source, string destination, string routingKey, IDictionary<string, object> arguments, bool waitConfirmation)
		{
			return _channel.ExchangeUnbind(source, destination, routingKey, arguments, waitConfirmation);
		}

		public Task ExchangeDelete(string exchange, IDictionary<string, object> arguments, bool waitConfirmation)
		{
			return _channel.ExchangeDelete(exchange, arguments, waitConfirmation);
		}

		public Task<AmqpQueueInfo> QueueDeclare(string queue, bool passive, bool durable, bool exclusive, bool autoDelete, IDictionary<string, object> arguments,
			bool waitConfirmation)
		{
			return _channel.QueueDeclare(queue, passive, durable, exclusive, autoDelete, arguments, waitConfirmation);
		}

		public Task QueueBind(string queue, string exchange, string routingKey, IDictionary<string, object> arguments, bool waitConfirmation)
		{
			return _channel.QueueBind(queue, exchange, routingKey, arguments, waitConfirmation);
		}

		public Task QueueUnbind(string queue, string exchange, string routingKey, IDictionary<string, object> arguments)
		{
			return _channel.QueueUnbind(queue, exchange, routingKey, arguments);
		}

		public Task QueueDelete(string queue, bool waitConfirmation)
		{
			return _channel.QueueDelete(queue, waitConfirmation);
		}

		public Task QueuePurge(string queue, bool waitConfirmation)
		{
			return _channel.QueuePurge(queue, waitConfirmation);
		}

		public TaskSlim BasicPublishWithConfirmation(string exchange, string routingKey, bool mandatory, BasicProperties properties,
			ArraySegment<byte> buffer)
		{
			return _channel.BasicPublishWithConfirmation(exchange, routingKey, mandatory, properties, buffer);
		}

		public TaskSlim BasicPublish(string exchange, string routingKey, bool mandatory, BasicProperties properties,
			ArraySegment<byte> buffer)
		{
			return _channel.BasicPublish(exchange, routingKey, mandatory, properties, buffer);
		}

		public void BasicPublishFast(string exchange, string routingKey, bool mandatory, BasicProperties properties,
			ArraySegment<byte> buffer)
		{
			_channel.BasicPublishFast(exchange, routingKey, mandatory, properties, buffer);
		}

		public Task<string> BasicConsume(ConsumeMode mode, QueueConsumer consumer, string queue, string consumerTag, bool withoutAcks,
			bool exclusive, IDictionary<string, object> arguments, bool waitConfirmation)
		{
			return _channel.BasicConsume(mode, consumer, queue, consumerTag, withoutAcks, exclusive, arguments, waitConfirmation);
		}

		public Task<string> BasicConsume(ConsumeMode mode, Func<MessageDelivery, Task> consumer, string queue, string consumerTag, bool withoutAcks, bool exclusive,
			IDictionary<string, object> arguments, bool waitConfirmation)
		{
			return _channel.BasicConsume(mode, consumer, queue, consumerTag, withoutAcks, exclusive, arguments, waitConfirmation);
		}

		public Task BasicCancel(string consumerTag, bool waitConfirmation)
		{
			return _channel.BasicCancel(consumerTag, waitConfirmation);
		}

		public Task BasicRecover(bool requeue)
		{
			return _channel.BasicRecover(requeue);
		}

		public Task<RpcHelper> CreateRpcHelper(ConsumeMode mode, int? timeoutInMs, int maxConcurrentCalls = 500)
		{
			return _channel.CreateRpcHelper(mode, timeoutInMs, maxConcurrentCalls);
		}

		public Task Close()
		{
			return _channel.Close();
		}
		
		#endregion

		#region Implementation of IDisposable

		public void Dispose()
		{
			_channel.Dispose();
		}

		#endregion

		internal void DoRecover()
		{

		}
	}
}