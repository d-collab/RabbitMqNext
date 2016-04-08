namespace RabbitMqNext.Recovery
{
	using System;
	using System.Collections.Generic;
	using System.Threading.Tasks;

	internal class ExchangeDeclaredRecovery
	{
		public ExchangeDeclaredRecovery(string exchange, string type, bool durable, bool autoDelete, IDictionary<string, object> arguments)
		{
		}

		public ExchangeDeclaredRecovery(string exchange, IDictionary<string, object> arguments)
		{
		}
	}

	internal class ExchangeBindRecovery
	{
		public ExchangeBindRecovery(string source, string destination, string routingKey, IDictionary<string, object> arguments)
		{

		}
	}

	internal class QueueDeclaredRecovery
	{
		public QueueDeclaredRecovery(string queue, bool passive, bool durable, bool exclusive, bool autoDelete, IDictionary<string, object> arguments)
		{

		}

		public QueueDeclaredRecovery(string queue)
		{
			
		}
	}

	internal class QueueBoundRecovery
	{
		public QueueBoundRecovery(string queue, string exchange, string routingKey, IDictionary<string, object> arguments)
		{
		}
	}

	internal class QueueConsumerRecovery
	{
		public QueueConsumerRecovery(ConsumeMode mode, Func<MessageDelivery, Task> consumer, string queue, string consumerTag2, bool withoutAcks, bool exclusive, IDictionary<string, object> arguments)
		{
		}

		public QueueConsumerRecovery(ConsumeMode mode, QueueConsumer consumer, string queue, string consumerTag2, bool withoutAcks, bool exclusive, IDictionary<string, object> arguments)
		{
		}

		private QueueConsumerRecovery(ConsumeMode mode, string queue, string consumerTag2, bool withoutAcks, bool exclusive, IDictionary<string, object> arguments)
		{
		}

		public QueueConsumerRecovery(string consumerTag)
		{
		}
	}
}