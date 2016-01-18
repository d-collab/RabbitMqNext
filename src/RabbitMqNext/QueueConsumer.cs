namespace RabbitMqNext
{
	using System.Threading.Tasks;

	public abstract class QueueConsumer
	{
		public abstract Task Consume(MessageDelivery delivery);

		// Add Connection lost, Cancelled, etc..
	}
}