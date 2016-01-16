namespace RabbitMqNext
{
	using System;
	using System.Collections.Generic;
	using System.Threading.Tasks;

	public enum ConsumeMode
	{
		SingleThreaded,
		ParallelWithReadBarrier,
		ParallelWithBufferCopy
	}

	public interface IAmqpConnection
	{
		
	}

	public interface IAmqpChannel
	{
		int ChannelNumber { get; }

		bool Closed { get; }
		
		Task BasicQos(uint prefetchSize, ushort prefetchCount, bool global);

		/// <summary>
		/// When sent by the client, this method acknowledges one or 
		/// more messages delivered via the Deliver or Get-Ok methods.
		/// </summary>
		/// <param name="deliveryTag">single message or a set of messages up to and including a specific message.</param>
		/// <param name="multiple">if true, up to and including a specific message</param>
		Task BasicAck(ulong deliveryTag, bool multiple);

		/// <summary>
		/// This method allows a client to reject one or more incoming messages. 
		/// It can be used to interrupt and cancel large incoming messages, or 
		/// return untreatable messages to their original queue.
		/// </summary>
		/// <param name="deliveryTag">single message or a set of messages up to and including a specific message.</param>
		/// <param name="multiple">if true, up to and including a specific message</param>
		/// <param name="requeue">If requeue is true, the server will attempt to requeue the message.  
		/// If requeue is false or the requeue  attempt fails the messages are discarded or dead-lettered.</param>
		Task BasicNAck(ulong deliveryTag, bool multiple, bool requeue);

		Task ExchangeDeclare(string exchange, string type, bool durable, bool autoDelete, IDictionary<string, object> arguments, bool waitConfirmation);
		
		Task<AmqpQueueInfo> QueueDeclare(string queue, bool passive, bool durable, bool exclusive, bool autoDelete, IDictionary<string, object> arguments, bool waitConfirmation);
		
		Task QueueBind(string queue, string exchange, string routingKey, IDictionary<string, object> arguments, bool waitConfirmation);

		TaskLight BasicPublish(string exchange, string routingKey, bool mandatory, bool immediate, 
			BasicProperties properties, ArraySegment<byte> buffer);

		void BasicPublishFast(string exchange, string routingKey, bool mandatory, bool immediate,
			BasicProperties properties, ArraySegment<byte> buffer);
		
//		Task<RpcHelper> CreateRpcHelper(ConsumeMode mode, int maxConcurrentCalls = 500);

		Task<string> BasicConsume(ConsumeMode mode, Func<MessageDelivery, Task> consumer,
			string queue, string consumerTag, bool withoutAcks, bool exclusive,
			IDictionary<string, object> arguments, bool waitConfirmation);

		Task BasicCancel(string consumerTag, bool waitConfirmation);
		
		Task Close();

		/// <summary>
		/// This method asks the server to redeliver all unacknowledged messages on a 
		/// specified channel. Zero or more messages may be redelivered.  This method
		/// </summary>
		/// <param name="requeue">  If this field is zero, the message will be redelivered to the original 
		/// recipient. If this bit is 1, the server will attempt to requeue the message, 
		/// potentially then delivering it to an alternative subscriber.</param>
		/// <returns></returns>
		Task BasicRecover(bool requeue);

		BasicProperties RentBasicProperties();

		void Return(BasicProperties prop);
	}
}