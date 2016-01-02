namespace RabbitMqNext.Internals
{
	using System;

	internal class CommandToSend
	{
		public ushort Channel;
		public ushort ClassId;
		public ushort MethodId;
		public Action<AmqpPrimitivesWriter, ushort, ushort, ushort> commandGenerator;
		public Action<ushort, int, AmqpError> ReplyAction2;
		public bool ExpectsReply;
	}
}