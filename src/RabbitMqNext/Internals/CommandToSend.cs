namespace RabbitMqNext.Internals
{
	using System;
	using System.Threading.Tasks;

	internal sealed class CommandToSend : IDisposable
	{
		private readonly Action<CommandToSend> _recycler;

		public CommandToSend(Action<CommandToSend> recycler)
		{
			_recycler = recycler;
		}

		public ushort Channel;
		public ushort ClassId;
		public ushort MethodId;
		public Action<AmqpPrimitivesWriter, ushort, ushort, ushort, object> commandGenerator;
		public Func<ushort, int, AmqpError, Task> ReplyAction;
		public bool ExpectsReply;
		public object OptionalArg;
		public TaskCompletionSource<bool> Tcs;
		public TaskLight TcsLight;

		public async Task ReplyAction3(ushort channel, int classMethodId, AmqpError error)
		{
			if (this.ReplyAction != null)
			{
				await this.ReplyAction(channel, classMethodId, error);
			}
			else
			{
				if (Tcs != null) 
					Tcs.SetResult(true);

				if (TcsLight != null)
					TcsLight.SetCompleted();
			}

			if (_recycler != null) _recycler(this);
		}

		public void Dispose()
		{
			Channel = ClassId = MethodId = 0;
			commandGenerator = null;
			ReplyAction = null;
			ExpectsReply = false;
			OptionalArg = null;
			Tcs = null;
			TcsLight = null;
		}
	}
}