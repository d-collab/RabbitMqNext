namespace RabbitMqNext
{
	using System;
	using System.Threading.Tasks;
	using Internals;

	public interface IConnection : IDisposable
	{
		void AddErrorCallback(Func<AmqpError, Task> errorCallback);
		void RemoveErrorCallback(Func<AmqpError, Task> errorCallback);

		/// <summary>
		/// Happens when the server tells the client to stop publishing
		/// </summary>
		event Action<string> ConnectionBlocked;

		/// <summary>
		/// Happens when the server allows the client to resume publishing
		/// </summary>
		event Action ConnectionUnblocked;

		bool IsClosed { get; }

		Task<IChannel> CreateChannel(ChannelOptions options = null);

		/// <summary>
		/// A channel with confirmation enabled will have the server ack'ing each message publish. 
		/// Should be used in conjunction with <see cref="IChannel.BasicPublishWithConfirmation"/>
		/// <para>
		/// <c>maxunconfirmedMessages</c> determines how many pending messages is allowed, before blocking publishing 
		/// until confirmation are received.
		/// </para>
		/// </summary>
		/// <param name="options"></param>
		/// <param name="maxunconfirmedMessages">The max number of unconfirmed message, before blocking</param>
		/// <returns></returns>
		Task<IChannel> CreateChannelWithPublishConfirmation(ChannelOptions options = null, int maxunconfirmedMessages = 100);
	}
}