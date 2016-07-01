namespace RabbitMqNext
{
	using System;
	using System.Collections.Generic;
	using System.Threading.Tasks;
	using Internals;


	public interface IChannel : IDisposable
	{
		void AddErrorCallback(Func<AmqpError, Task> errorCallback);
		void RemoveErrorCallback(Func<AmqpError, Task> errorCallback);

		/// <summary>
		/// Happens when the server tells the client to stop publishing (channelflow or connectionblock)
		/// </summary>
		event Action<string> ChannelBlocked;

		/// <summary>
		/// Happens when the server allows the client to resume publishing (channelflow or connectionunblock)
		/// </summary>
		event Action ChannelUnblocked;

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

		Task/*TaskSlim*/ BasicPublishWithConfirmation(string exchange, string routingKey, bool mandatory,
			BasicProperties properties, ArraySegment<byte> buffer);

		Task/*TaskSlim*/ BasicPublish(string exchange, string routingKey, bool mandatory, 
			BasicProperties properties, ArraySegment<byte> buffer);

		// This would be a better approach: allocation of a delegate vs allocation of a buffer
//		TaskSlim BasicPublish(string exchange, string routingKey, bool mandatory,
//			BasicProperties properties, Action<Stream> bodyWriter);

		void BasicPublishFast(string exchange, string routingKey, bool mandatory, 
			BasicProperties properties, ArraySegment<byte> buffer);

		Task<string> BasicConsume(ConsumeMode mode, IQueueConsumer consumer,
			string queue, string consumerTag, bool withoutAcks, bool exclusive,
			IDictionary<string, object> arguments, bool waitConfirmation);

		Task<string> BasicConsume(ConsumeMode mode, Func<MessageDelivery, Task> consumer,
			string queue, string consumerTag, bool withoutAcks, bool exclusive,
			IDictionary<string, object> arguments, bool waitConfirmation);

		/// <summary>
		/// Cancels a consumer.
		/// </summary>
		/// <param name="consumerTag"></param>
		/// <param name="waitConfirmation"></param>
		/// <returns></returns>
		Task BasicCancel(string consumerTag, bool waitConfirmation);
		
		/// <summary>
		/// This method asks the server to redeliver all unacknowledged messages on a specified channel. Zero or more messages may be redelivered.
		/// </summary>
		/// <param name="requeue">If false, the message will 
		/// be redelivered to the original recipient. If true, the 
		/// server will attempt to requeue the message, potentially then delivering it to an alternative subscriber.
		/// </param>
		/// <returns></returns>
		Task BasicRecover(bool requeue);

		Task<RpcHelper> CreateRpcHelper(ConsumeMode mode, int? timeoutInMs, int maxConcurrentCalls = 500, bool captureContext = false);

		Task<RpcAggregateHelper> CreateRpcAggregateHelper(ConsumeMode mode, int? timeoutInMs, int maxConcurrentCalls = 500, bool captureContext = false);
		
		Task Close();
	}
}