namespace RabbitMqNext
{
	using System;
	using System.Collections.Generic;
	using System.IO;
	using System.Threading.Tasks;
	using Internals;

	public interface IChannel : IDisposable
	{
		event Action<AmqpError> OnError;
		Func<UndeliveredMessage, Task> MessageUndeliveredHandler { get; set; }

		bool IsConfirmationEnabled { get; }
		
		ushort ChannelNumber { get; }
		
		bool IsClosed { get; }
		
		BasicProperties RentBasicProperties();
		
		void Return(BasicProperties properties);
		
		Task BasicQos(uint prefetchSize, ushort prefetchCount, bool global);
		
		void BasicAck(ulong deliveryTag, bool multiple);
		
		void BasicNAck(ulong deliveryTag, bool multiple, bool requeue);

		Task ExchangeDeclare(string exchange, string type, bool durable, bool autoDelete,
			IDictionary<string, object> arguments, bool waitConfirmation);

		Task ExchangeBind(string source, string destination, string routingKey, 
			IDictionary<string, object> arguments, bool waitConfirmation);

		Task ExchangeUnbind(string source, string destination, string routingKey,
			IDictionary<string, object> arguments, bool waitConfirmation);

		Task ExchangeDelete(string exchange, IDictionary<string, object> arguments, bool waitConfirmation);

		Task<AmqpQueueInfo> QueueDeclare(string queue, bool passive, bool durable, bool exclusive, bool autoDelete,
			IDictionary<string, object> arguments, bool waitConfirmation);

		Task QueueBind(string queue, string exchange, string routingKey, IDictionary<string, object> arguments,
			bool waitConfirmation);

		Task QueueUnbind(string queue, string exchange, string routingKey, IDictionary<string, object> arguments);
		Task QueueDelete(string queue /*, bool ifUnused, bool ifEmpty*/, bool waitConfirmation);
		Task QueuePurge(string queue, bool waitConfirmation);

		TaskSlim BasicPublishWithConfirmation(string exchange, string routingKey, bool mandatory,
			BasicProperties properties, ArraySegment<byte> buffer);

		TaskSlim BasicPublish(string exchange, string routingKey, bool mandatory, 
			BasicProperties properties, ArraySegment<byte> buffer);

		// This would be a better approach: allocation of a delegate vs allocation of a buffer
//		TaskSlim BasicPublish(string exchange, string routingKey, bool mandatory,
//			BasicProperties properties, Action<Stream> bodyWriter);

		void BasicPublishFast(string exchange, string routingKey, bool mandatory, 
			BasicProperties properties, ArraySegment<byte> buffer);

		Task<string> BasicConsume(ConsumeMode mode, QueueConsumer consumer,
			string queue, string consumerTag, bool withoutAcks, bool exclusive,
			IDictionary<string, object> arguments, bool waitConfirmation);

		Task<string> BasicConsume(ConsumeMode mode, Func<MessageDelivery, Task> consumer,
			string queue, string consumerTag, bool withoutAcks, bool exclusive,
			IDictionary<string, object> arguments, bool waitConfirmation);

		Task BasicCancel(string consumerTag, bool waitConfirmation);
		Task BasicRecover(bool requeue);
		Task<RpcHelper> CreateRpcHelper(ConsumeMode mode, int? timeoutInMs, int maxConcurrentCalls = 500);
		Task<RpcAggregateHelper> CreateRpcAggregateHelper(ConsumeMode mode, int? timeoutInMs, int maxConcurrentCalls = 500);
		Task Close();
	}
}