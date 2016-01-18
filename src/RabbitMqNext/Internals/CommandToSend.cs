namespace RabbitMqNext.Internals
{
	using System;
	using System.Runtime.CompilerServices;
	using System.Threading.Tasks;
	using RabbitMqNext.Io;
	using RabbitMqNext.Internals;

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
		public Action PrepareAction;
		public bool ExpectsReply;
		public object OptionalArg;
		public TaskCompletionSource<bool> Tcs;
		public TaskSlim TcsSlim;

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		internal void Prepare()
		{
			if (PrepareAction != null) PrepareAction();
		}

		public async Task RunReplyAction(ushort channel, int classMethodId, AmqpError error)
		{
			if (this.ReplyAction != null)
			{
				await this.ReplyAction(channel, classMethodId, error);
			}
			else
			{
				if (error != null)
				{
					AmqpIOBase.SetException(Tcs, error, classMethodId);
					AmqpIOBase.SetException(TcsSlim, error, classMethodId);
				}
				else
				{
					if (Tcs != null)
						Tcs.SetResult(true);

					if (TcsSlim != null)
						TcsSlim.SetCompleted();
				}
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
			PrepareAction = null;
			Tcs = null;
			TcsSlim = null;
		}
	}
}