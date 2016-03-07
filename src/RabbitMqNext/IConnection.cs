namespace RabbitMqNext
{
	using System;
	using System.Threading.Tasks;
	using Internals;

	public interface IConnection : IDisposable
	{
		event Action<AmqpError> OnError;
		
		bool IsClosed { get; }

		Task<IChannel> CreateChannel();
		
		Task<IChannel> CreateChannelWithPublishConfirmation(int maxunconfirmedMessages = 100);
	}
}