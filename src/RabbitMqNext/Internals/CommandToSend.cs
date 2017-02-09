namespace RabbitMqNext.Internals
{
	using System;
	using System.ComponentModel;
	using System.Diagnostics;
	using System.Runtime.CompilerServices;
	using System.Threading;
	using System.Threading.Tasks;
	using RabbitMqNext.Io;
	using RabbitMqNext.Internals;

	[DebuggerDisplay("CommandToSend Channel: {Channel} Class: {ClassId} Method: {MethodId} ExpectsReply: {ExpectsReply}")]
	internal sealed class CommandToSend : IDisposable, ISupportInitialize
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
		public Action<ushort, int, AmqpError> ReplyAction;
		public Action PrepareAction;
		public bool ExpectsReply;
		public object OptionalArg;
		public TaskCompletionSource<bool> Tcs;
		public bool Immediately;

		private ManualResetEventSlim _whenReplyReceived;
		private int _inUse = 0;

//		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		internal void Prepare(ManualResetEventSlim whenReplyReceived)
		{
			AssertCanBeUsed();

			_whenReplyReceived = whenReplyReceived;

			if (PrepareAction != null) PrepareAction();
		}

		public void RunReplyAction(ushort channel, int classMethodId, AmqpError error)
		{
			AssertCanBeUsed();

#if DEBUG
			if (classMethodId != 0)
			{
				// Confirm reply
				var classId = classMethodId >> 16;
				var methodId = classMethodId & 0x0000FFFF;

				var matchesClass = (ClassId == classId);
				var matchesMethod = MethodId == (methodId - 1);

				if (!matchesClass || !matchesMethod)
				{
					if (LogAdapter.ExtendedLogEnabled)
						LogAdapter.LogDebug("CommandToSend", "[channel " + channel + "] Command for " + ClassId + "|" + MethodId + " did not match reply " + classId + "|" + methodId);
				}
			}
#endif
			try
			{

				// Allows more commands to be sent. This contention is sadly required by the amqp/rabbitmq
				if (_whenReplyReceived != null)
				{
					_whenReplyReceived.Set();
				}
				// ---

				if (this.ReplyAction != null)
				{
					try
					{
						this.ReplyAction(channel, classMethodId, error);
					}
					catch (Exception ex)
					{
						error = new AmqpError
						{
							ReplyText = ex.Message
						};
						AmqpIOBase.SetException(Tcs, error, classMethodId);

						throw;
					}
				}
				else
				{
					if (error != null)
					{
						AmqpIOBase.SetException(Tcs, error, classMethodId);
					}
					else
					{
						if (Tcs != null)
							Tcs.TrySetResult(true);
					}
				}
			}
			finally
			{
				if (_recycler != null) _recycler(this);
			}
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
			_whenReplyReceived = null;

			if (Interlocked.CompareExchange(ref _inUse, value: NotInUseConst, comparand: InUseConst) != InUseConst)
				throw new Exception("CommandToSend being shared inadvertently 1");
		}

		// ISupportInitialize start

		public void BeginInit()
		{
			if (Interlocked.CompareExchange(ref _inUse, value: InUseConst, comparand: NotInUseConst) != NotInUseConst)
				throw new Exception("CommandToSend being shared inadvertently 2");
		}

		private const int InUseConst = 1;
		private const int NotInUseConst = 0;

		public void EndInit()
		{
		}

		// end

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		private void AssertCanBeUsed()
		{
			if (Volatile.Read(ref _inUse) != InUseConst)
			{
				throw new Exception("Cannot use a recycled obj - Channel " + this.Channel + 
					" Class " + this.ClassId + 
					" Method " + this.MethodId + 
					" Opt: " + this.OptionalArg);
			}
		}
	}
}