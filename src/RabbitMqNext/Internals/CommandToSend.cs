namespace RabbitMqNext.Internals
{
	using System;

	internal class CommandToSend
	{
		public Action<AmqpPrimitivesWriter> commandGenerator;
		public Action<ushort, int> ReplyAction;
		public bool ExpectsReply;
	}
}