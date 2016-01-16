namespace RabbitMqNext
{
	using System;

	public class Channel //: CommonCommandSender, IAmqpChannel
	{
		internal Connection2 _connection;
		// Only non null when the channel is in pub-confirm mode
		internal MessagesPendingConfirmationKeeper _confirmationKeeper;

		internal ChannelIO _io;
		internal ushort _channelNum;

		public Channel()
		{

			_io = new ChannelIO(this);
		}

//		public void EnablePublishConfirm()
//		{
//			if (_confirmationKeeper != null) throw new Exception("Already set");
//
//			_confirmationKeeper = new MessagesPendingConfirmationKeeper(
//				maxunconfirmedMessages,
//				_connection._cancellationToken);
//
//		}
	}
}
