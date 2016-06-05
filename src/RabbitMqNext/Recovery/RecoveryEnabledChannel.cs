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
		const string LogSource = "RecoveryEnabledChannel";
		
		private Channel _channel;
		private readonly AutoRecoverySettings _recoverySettings;

		private QosSettingRecovery? _qosSetting;
		private readonly List<ExchangeDeclaredRecovery> _declaredExchanges;
		private readonly List<ExchangeBindRecovery> _boundExchanges;
		private readonly List<QueueDeclaredRecovery> _declaredQueues;
		private readonly List<QueueBoundRecovery> _boundQueues;
		private readonly List<BaseRpcHelper> _rpcHelpers;
		private readonly List<QueueConsumerRecovery> _consumersRegistered;

		private int _isRecovering;

		public RecoveryEnabledChannel(Channel channel, AutoRecoverySettings recoverySettings)
		{
			_channel = channel;
			_recoverySettings = recoverySettings;

			_declaredExchanges = new List<ExchangeDeclaredRecovery>();
			_boundExchanges = new List<ExchangeBindRecovery>();
			_declaredQueues = new List<QueueDeclaredRecovery>();
			_boundQueues = new List<QueueBoundRecovery>();
			_consumersRegistered = new List<QueueConsumerRecovery>();
			_rpcHelpers = new List<BaseRpcHelper>();
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

			await _channel.BasicQos(prefetchSize, prefetchCount, global).ConfigureAwait(false);

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

			await _channel.ExchangeDeclare(exchange, type, durable, autoDelete, arguments, waitConfirmation).ConfigureAwait(false);

			var recovery = new ExchangeDeclaredRecovery(exchange, type, durable, autoDelete, arguments);

			lock(_declaredExchanges) _declaredExchanges.Add(recovery);
		}

		public async Task ExchangeBind(string source, string destination, string routingKey, IDictionary<string, object> arguments, bool waitConfirmation)
		{
			ThrowIfRecoveryInProcess();

			await _channel.ExchangeBind(source, destination, routingKey, arguments, waitConfirmation).ConfigureAwait(false);

			var recovery = new ExchangeBindRecovery(source, destination, routingKey, arguments);

			lock (_boundExchanges) _boundExchanges.Add(recovery);
		}

		public async Task ExchangeUnbind(string source, string destination, string routingKey, IDictionary<string, object> arguments, bool waitConfirmation)
		{
			ThrowIfRecoveryInProcess();

			await _channel.ExchangeUnbind(source, destination, routingKey, arguments, waitConfirmation).ConfigureAwait(false);

			var recovery = new ExchangeBindRecovery(source, destination, routingKey, arguments);

			lock (_boundExchanges) _boundExchanges.Remove(recovery);
		}

		public async Task ExchangeDelete(string exchange, IDictionary<string, object> arguments, bool waitConfirmation)
		{
			ThrowIfRecoveryInProcess();

			var recovery = new ExchangeDeclaredRecovery(exchange, arguments);

			lock(_declaredExchanges) _declaredExchanges.Remove(recovery);

			await _channel.ExchangeDelete(exchange, arguments, waitConfirmation).ConfigureAwait(false);
		}

		public async Task<AmqpQueueInfo> QueueDeclare(string queue, bool passive, bool durable, bool exclusive, bool autoDelete, IDictionary<string, object> arguments,
			bool waitConfirmation)
		{
			ThrowIfRecoveryInProcess();

			var result = await _channel.QueueDeclare(queue, passive, durable, exclusive, autoDelete, arguments, waitConfirmation).ConfigureAwait(false);

			// We can't try to restore an entity with a reserved name (will happen for temporary queues)
			if (result != null && result.Name.StartsWith(AmqpConstants.AmqpReservedPrefix, StringComparison.Ordinal))
			{
				return result;
			}

			lock (_declaredQueues) _declaredQueues.Add(new QueueDeclaredRecovery(result.Name, passive, durable, exclusive, autoDelete, arguments));

			return result;
		}

		public async Task QueueBind(string queue, string exchange, string routingKey, IDictionary<string, object> arguments, bool waitConfirmation)
		{
			ThrowIfRecoveryInProcess();

			await _channel.QueueBind(queue, exchange, routingKey, arguments, waitConfirmation).ConfigureAwait(false);

			if (_recoverySettings.RecoverBindings)
			{
				lock (_boundQueues) _boundQueues.Add(new QueueBoundRecovery(queue, exchange, routingKey, arguments));
			}
		}

		public Task QueueUnbind(string queue, string exchange, string routingKey, IDictionary<string, object> arguments)
		{
			ThrowIfRecoveryInProcess();

			if (_recoverySettings.RecoverBindings)
			{
				lock (_boundQueues) _boundQueues.Remove(new QueueBoundRecovery(queue, exchange, routingKey, arguments));
			}
			
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

			var consumerTag2 = await _channel.BasicConsume(mode, consumer, queue, consumerTag, withoutAcks, exclusive, arguments, waitConfirmation).ConfigureAwait(false);

			lock (_consumersRegistered) _consumersRegistered.Add(new QueueConsumerRecovery(mode, consumer, queue, consumerTag2, withoutAcks, exclusive, arguments));

			return consumerTag2;
		}

		public async Task<string> BasicConsume(ConsumeMode mode, Func<MessageDelivery, Task> consumer, string queue, string consumerTag, bool withoutAcks, bool exclusive,
			IDictionary<string, object> arguments, bool waitConfirmation)
		{
			ThrowIfRecoveryInProcess();

			var consumerTag2 = await _channel.BasicConsume(mode, consumer, queue, consumerTag, withoutAcks, exclusive, arguments, waitConfirmation).ConfigureAwait(false);

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

			if (this.IsConfirmationEnabled) throw new Exception("This channel is set up for confirmations");

			var helper = await _channel.CreateRpcHelper(mode, timeoutInMs, maxConcurrentCalls).ConfigureAwait(false);

			lock (_rpcHelpers) _rpcHelpers.Add(helper);

			return helper;
		}

		public async Task<RpcAggregateHelper> CreateRpcAggregateHelper(ConsumeMode mode, int? timeoutInMs, int maxConcurrentCalls)
		{
			ThrowIfRecoveryInProcess();

			var helper = await _channel.CreateRpcAggregateHelper(mode, timeoutInMs, maxConcurrentCalls).ConfigureAwait(false);

			lock (_rpcHelpers) _rpcHelpers.Add(helper);

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
			Interlocked.Exchange(ref _isRecovering, 1);

			lock (_rpcHelpers)
			{
				foreach (var helper in _rpcHelpers)
				{
					helper.SignalInRecovery();
				}
			}

			foreach (var consumer in _consumersRegistered)
			{
				consumer.SignalBlocked();
			}
		}

		internal async Task DoRecover(Connection connection)
		{
			try
			{
				var maxUnconfirmed = this._channel._confirmationKeeper != null ? (int) this._channel._confirmationKeeper.Max : 0;

				var replacementChannel = (Channel) await connection.InternalCreateChannel(this.ChannelNumber, maxUnconfirmed, this.IsConfirmationEnabled).ConfigureAwait(false);

				// _channel.Dispose(); need to dispose in a way that consumers do not receive the cancellation signal, but drain any pending task

				// copy delegate pointers from old to new
				_channel.CopyDelegates(replacementChannel);

				// 0. QoS
				await RecoverQos(replacementChannel).ConfigureAwait(false);

				// 1. Recover exchanges
				await RecoverExchanges(replacementChannel).ConfigureAwait(false);

				// 2. Recover queues
				await RecoverQueues(replacementChannel).ConfigureAwait(false);

				// 3. Recover bindings (queue and exchanges)
				await RecoverBindings(replacementChannel).ConfigureAwait(false);

				// 4. Recover consumers 
				await RecoverConsumers(replacementChannel).ConfigureAwait(false);

				// (+ rpc lifecycle)
				foreach (var helper in _rpcHelpers)
				{
					await helper.SignalRecovered(replacementChannel).ConfigureAwait(false);
				}
			
				_channel = replacementChannel;

				Interlocked.Exchange(ref _isRecovering, 0);
			}
			catch (Exception ex)
			{
				LogAdapter.LogError(LogSource, "Recovery error", ex);

				throw;
			}
		}

		private async Task RecoverQos(Channel newChannel)
		{
			if (_qosSetting.HasValue)
			{
				await _qosSetting.Value.Apply(newChannel).ConfigureAwait(false);
			}
		}

		private async Task RecoverConsumers(Channel newChannel)
		{
			foreach (var consumer in _consumersRegistered)
			{
				await consumer.Apply(newChannel).ConfigureAwait(false);
			}
		}

		private async Task RecoverBindings(Channel newChannel)
		{
			// 3. Recover bindings (exchanges)
			foreach (var binding in _boundExchanges)
			{
				await binding.Apply(newChannel).ConfigureAwait(false);
			}

			// 3. Recover bindings (queues)
			foreach (var binding in _boundQueues)
			{
				await binding.Apply(newChannel).ConfigureAwait(false);
			}
		}

		private async Task RecoverQueues(Channel newChannel)
		{
			foreach (var declaredQueue in _declaredQueues)
			{
				await declaredQueue.Apply(newChannel).ConfigureAwait(false);
			}
		}

		private async Task RecoverExchanges(Channel newChannel)
		{
			foreach (var declaredExchange in _declaredExchanges)
			{
				await declaredExchange.Apply(newChannel).ConfigureAwait(false);
			}
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		private void ThrowIfRecoveryInProcess()
		{
			if (_isRecovering == 1) throw new Exception("Recovery in progress, channel not available");
		}
	}
}