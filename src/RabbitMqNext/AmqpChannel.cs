namespace RabbitMqNext
{
	using System;
	using System.Collections.Generic;
	using System.Threading.Tasks;
	using Internals;

	public interface IAmqpChannel
	{
		int ChannelNumber { get; }

		Task BasicQos(uint prefetchSize, ushort prefetchCount, bool global);

		Task ExchangeDeclare(string exchange, string type, bool durable, bool autoDelete,
			IDictionary<string, object> arguments, bool waitConfirmation);

		Task BasicAck(ulong deliveryTag, bool multiple);
	}

	internal class AmqpChannel : IAmqpChannel
	{
		private readonly ushort _channelNum;
		private readonly ConnectionStateMachine _connection;

		public AmqpChannel(ushort channelNum, ConnectionStateMachine connection)
		{
			_channelNum = channelNum;
			_connection = connection;
		}

		public int ChannelNumber { get { return _channelNum; } }

		internal Task Open()
		{
			var tcs = new TaskCompletionSource<bool>();

			var writer = AmqpChannelLevelFrameWriter.ChannelOpen();

			_connection.SendCommand(_channelNum, 20, 10, writer,
				reply: async (channel, classMethodId, error) =>
				{
					if (classMethodId == AmqpClassMethodChannelLevelConstants.ChannelOpenOk)
					{
						await _connection._frameReader.Read_ChannelOpenOk((reserved) =>
						{
							tcs.SetResult(true);
						});
					}
					else
					{
						Util.SetException(tcs, error, classMethodId);
					}
				}, expectsReply: true);

			return tcs.Task;
		}

		

		public Task BasicQos(uint prefetchSize, ushort prefetchCount, bool global)
		{
			var tcs = new TaskCompletionSource<bool>();
			return tcs.Task;
		}

		public Task BasicAck(ulong deliveryTag, bool multiple)
		{
			var tcs = new TaskCompletionSource<bool>();
			return tcs.Task;
		}

		public Task ExchangeDeclare(string exchange, string type, bool durable, bool autoDelete, 
									IDictionary<string, object> arguments, bool waitConfirmation)
		{
			var tcs = new TaskCompletionSource<bool>();
			return tcs.Task;
		}

		public Task BasicPublish(string exchange, string routingKey, bool mandatory, bool immediate, 
								 BasicProperties properties, ArraySegment<byte> buffer)
		{
			var tcs = new TaskCompletionSource<bool>();

			return tcs.Task;
		}
	}
}