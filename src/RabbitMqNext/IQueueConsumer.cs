namespace RabbitMqNext
{
	using System.Threading.Tasks;

	public interface IQueueConsumer
	{
		Task Consume(MessageDelivery delivery);

		/// <summary>
		/// When auto-recovery is enabled, indicates that the connection is down but recovery will start
		/// </summary>
		void Broken();

		/// <summary>
		/// When auto-recovery is enabled, indicates that recovery has succeeded
		/// </summary>
		void Recovered();

		/// <summary>
		/// indicates that the server cancelled this consumer
		/// </summary>
		void Cancelled();
	}

	public abstract class QueueConsumer : IQueueConsumer
	{
		public abstract Task Consume(MessageDelivery delivery);

		public virtual void Broken()
		{
		}

		public virtual void Recovered()
		{
		}

		public virtual void Cancelled()
		{
		}
	}
}