namespace RabbitMqNext.Recovery
{
	using System;
	using System.Collections.Generic;
	using System.Runtime.CompilerServices;
	using System.Threading;
	using System.Threading.Tasks;
	using Internals;


	public class RecoveryEnabledChannel : IChannel
	{
		const string LogSource = "ChannelRecovery";
		
		private Channel _channel;

		private QosSettingRecovery? _qosSetting;
		private readonly List<ExchangeDeclaredRecovery> _declaredExchanges;
		private readonly List<ExchangeBindRecovery> _boundExchanges;
		private readonly List<QueueDeclaredRecovery> _declaredQueues;
		private readonly List<QueueBoundRecovery> _boundQueues;
		private readonly List<RpcHelper> _rpcHelpers;
		private readonly List<RpcAggregateHelper> _rpcAggregateHelpers;
		private readonly List<QueueConsumerRecovery> _consumersRegistered;

		private bool _isRecovering;

		public RecoveryEnabledChannel(Channel channel)
		{
			_channel = channel;

			_declaredExchanges = new List<ExchangeDeclaredRecovery>();
			_boundExchanges = new List<ExchangeBindRecovery>();
			_declaredQueues = new List<QueueDeclaredRecovery>();
			_boundQueues = new List<QueueBoundRecovery>();
			_consumersRegistered = new List<QueueConsumerRecovery>();
			_rpcHelpers = new List<RpcHelper>();
			_rpcAggregateHelpers = new List<RpcAggregateHelper>();
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

		public async Task BasicQos(uint prefetchSize, ushort prefetchCount, bool global)
		{
			ThrowIfRecoveryInProcess();

			await _channel.BasicQos(prefetchSize, prefetchCount, global);

			_qosSetting = new QosSettingRecovery(prefetchSize, prefetchCount, global);
		}

		public void BasicAck(ulong deliveryTag, bool multiple)
		{
			ThrowIfRecoveryInProcess();

			_channel.BasicAck(deliveryTag, multiple);
		}

		public void BasicNAck(ulong deliveryTag, bool multiple, bool requeue)
		{
			ThrowIfRecoveryInProcess();

			_channel.BasicNAck(deliveryTag, multiple, requeue);
		}

		public async Task ExchangeDeclare(string exchange, string type, bool durable, bool autoDelete, IDictionary<string, object> arguments, bool waitConfirmation)
		{
			ThrowIfRecoveryInProcess();

			await _channel.ExchangeDeclare(exchange, type, durable, autoDelete, arguments, waitConfirmation);

			var recovery = new ExchangeDeclaredRecovery(exchange, type, durable, autoDelete, arguments);

			lock(_declaredExchanges) _declaredExchanges.Add(recovery);
		}

		public async Task ExchangeBind(string source, string destination, string routingKey, IDictionary<string, object> arguments, bool waitConfirmation)
		{
			ThrowIfRecoveryInProcess();

			await _channel.ExchangeBind(source, destination, routingKey, arguments, waitConfirmation);

			var recovery = new ExchangeBindRecovery(source, destination, routingKey, arguments);

			lock(_boundExchanges) _boundExchanges.Add(recovery);
		}

		public async Task ExchangeUnbind(string source, string destination, string routingKey, IDictionary<string, object> arguments, bool waitConfirmation)
		{
			ThrowIfRecoveryInProcess();

			await _channel.ExchangeUnbind(source, destination, routingKey, arguments, waitConfirmation);

			var recovery = new ExchangeBindRecovery(source, destination, routingKey, arguments);

			lock(_boundExchanges) _boundExchanges.Remove(recovery);
		}

		public Task ExchangeDelete(string exchange, IDictionary<string, object> arguments, bool waitConfirmation)
		{
			ThrowIfRecoveryInProcess();

			var recovery = new ExchangeDeclaredRecovery(exchange, arguments);

			lock(_declaredExchanges) _declaredExchanges.Remove(recovery);
			
			return _channel.ExchangeDelete(exchange, arguments, waitConfirmation);
		}

		public async Task<AmqpQueueInfo> QueueDeclare(string queue, bool passive, bool durable, bool exclusive, bool autoDelete, IDictionary<string, object> arguments,
			bool waitConfirmation)
		{
			ThrowIfRecoveryInProcess();

			var result = await _channel.QueueDeclare(queue, passive, durable, exclusive, autoDelete, arguments, waitConfirmation);

			lock (_declaredQueues) _declaredQueues.Add(new QueueDeclaredRecovery(result.Name, passive, durable, exclusive, autoDelete, arguments));

			return result;
		}

		public async Task QueueBind(string queue, string exchange, string routingKey, IDictionary<string, object> arguments, bool waitConfirmation)
		{
			ThrowIfRecoveryInProcess();

			await _channel.QueueBind(queue, exchange, routingKey, arguments, waitConfirmation);

			lock(_boundQueues) _boundQueues.Add(new QueueBoundRecovery(queue, exchange, routingKey, arguments));
		}

		public Task QueueUnbind(string queue, string exchange, string routingKey, IDictionary<string, object> arguments)
		{
			ThrowIfRecoveryInProcess();

			lock (_boundQueues) _boundQueues.Remove(new QueueBoundRecovery(queue, exchange, routingKey, arguments));

			return _channel.QueueUnbind(queue, exchange, routingKey, arguments);
		}

		public Task QueueDelete(string queue, bool waitConfirmation)
		{
			ThrowIfRecoveryInProcess();

			lock (_declaredQueues) _declaredQueues.Remove(new QueueDeclaredRecovery(queue));

			return _channel.QueueDelete(queue, waitConfirmation);
		}

		public Task QueuePurge(string queue, bool waitConfirmation)
		{
			ThrowIfRecoveryInProcess();

			return _channel.QueuePurge(queue, waitConfirmation);
		}

		public TaskSlim BasicPublishWithConfirmation(string exchange, string routingKey, bool mandatory, BasicProperties properties,
			ArraySegment<byte> buffer)
		{
			ThrowIfRecoveryInProcess();

			return _channel.BasicPublishWithConfirmation(exchange, routingKey, mandatory, properties, buffer);
		}

		public TaskSlim BasicPublish(string exchange, string routingKey, bool mandatory, BasicProperties properties,
			ArraySegment<byte> buffer)
		{
			ThrowIfRecoveryInProcess();

			return _channel.BasicPublish(exchange, routingKey, mandatory, properties, buffer);
		}

		public void BasicPublishFast(string exchange, string routingKey, bool mandatory, BasicProperties properties,
			ArraySegment<byte> buffer)
		{
			ThrowIfRecoveryInProcess();

			_channel.BasicPublishFast(exchange, routingKey, mandatory, properties, buffer);
		}

		public async Task<string> BasicConsume(ConsumeMode mode, IQueueConsumer consumer, string queue, string consumerTag, bool withoutAcks,
			bool exclusive, IDictionary<string, object> arguments, bool waitConfirmation)
		{
			ThrowIfRecoveryInProcess();

			var consumerTag2 = await _channel.BasicConsume(mode, consumer, queue, consumerTag, withoutAcks, exclusive, arguments, waitConfirmation);

			lock (_consumersRegistered) _consumersRegistered.Add(new QueueConsumerRecovery(mode, consumer, queue, consumerTag2, withoutAcks, exclusive, arguments));

			return consumerTag2;
		}

		public async Task<string> BasicConsume(ConsumeMode mode, Func<MessageDelivery, Task> consumer, string queue, string consumerTag, bool withoutAcks, bool exclusive,
			IDictionary<string, object> arguments, bool waitConfirmation)
		{
			ThrowIfRecoveryInProcess();

			var consumerTag2 = await _channel.BasicConsume(mode, consumer, queue, consumerTag, withoutAcks, exclusive, arguments, waitConfirmation);

			lock (_consumersRegistered) _consumersRegistered.Add(new QueueConsumerRecovery(mode, consumer, queue, consumerTag2, withoutAcks, exclusive, arguments));

			return consumerTag2;
		}

		public Task BasicCancel(string consumerTag, bool waitConfirmation)
		{
			ThrowIfRecoveryInProcess();

			lock (_consumersRegistered) _consumersRegistered.Remove(new QueueConsumerRecovery(consumerTag));

			return _channel.BasicCancel(consumerTag, waitConfirmation);
		}

		public Task BasicRecover(bool requeue)
		{
			ThrowIfRecoveryInProcess();

			return _channel.BasicRecover(requeue);
		}

		public async Task<RpcHelper> CreateRpcHelper(ConsumeMode mode, int? timeoutInMs, int maxConcurrentCalls)
		{
			ThrowIfRecoveryInProcess();

			var helper = await _channel.CreateRpcHelper(mode, timeoutInMs, maxConcurrentCalls);

			lock (_rpcHelpers) _rpcHelpers.Add(helper);

			return helper;
		}

		public async Task<RpcAggregateHelper> CreateRpcAggregateHelper(ConsumeMode mode, int? timeoutInMs, int maxConcurrentCalls)
		{
			ThrowIfRecoveryInProcess();

			var helper = await _channel.CreateRpcAggregateHelper(mode, timeoutInMs, maxConcurrentCalls);

			lock (_rpcAggregateHelpers) _rpcAggregateHelpers.Add(helper);

			return helper;
		}

		public Task Close()
		{
			// empty everything

			return _channel.Close();
		}
		
		#endregion

		#region Implementation of IDisposable

		public void Dispose()
		{
			_channel.Dispose();
		}

		#endregion

		internal void Disconnected()
		{
			_isRecovering = true;
			Thread.MemoryBarrier();
		}

		internal async Task DoRecover(Connection connection)
		{
			var maxUnconfirmed = this._channel._confirmationKeeper != null ? (int) this._channel._confirmationKeeper.Max : 0;

			var replacementChannel = (Channel) await connection.InternalCreateChannel(this.ChannelNumber, maxUnconfirmed, this.IsConfirmationEnabled);

			// _channel.Dispose(); need to dispose in a way that consumers do not receive the cancellation signal, but drain any pending task

			_channel = replacementChannel;

			// TODO: copy delegate pointers from old to new

			// 1. Recover exchanges + exchange bindings
			// 2. Recover queues
			// 3. Recover bindings
			// 4. Recover consumers

			_isRecovering = false;
			Thread.MemoryBarrier();
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		private void ThrowIfRecoveryInProcess()
		{
			if (_isRecovering) throw new Exception("Recovery in progress, channel not available");
		}
	}
}