namespace RabbitMqNext
{
	using System.Threading.Tasks;

	public abstract class QueueConsumer
	{
		public abstract Task Consume(MessageDelivery delivery);

		// Add Connection lost, Cancelled, etc..

		/// <summary>
		/// When auto-recovery is enabled, indicates that the connection is down but recovery will start
		/// </summary>
		public virtual void Broken()
		{
		}

		/// <summary>
		/// When auto-recovery is enabled, indicates that recovery has succeeded
		/// </summary>
		public virtual void Recovered()
		{
		}

		/// <summary>
		/// indicates that the server cancelled this consumer
		/// </summary>
		public virtual void Cancelled()
		{
		}
	}
}