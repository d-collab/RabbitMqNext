namespace RabbitMqNext.Internals
{
	using System;
	using System.Threading.Tasks;

	internal sealed class CommandToSend
	{
		public ushort Channel;
		public ushort ClassId;
		public ushort MethodId;
		public Action<AmqpPrimitivesWriter, ushort, ushort, ushort, object> commandGenerator;
		public Action<ushort, int, AmqpError> ReplyAction;
		public bool ExpectsReply;
		public object OptionalArg;

		public TaskCompletionSource<bool> Tcs;

		public void ReplyAction3(ushort channel, int classMethodId, AmqpError error)
		{
			if (this.ReplyAction != null)
			{
				this.ReplyAction(channel, classMethodId, error);
			}
			else
			{
				Tcs.SetResult(true);
			}
		}
	}
}