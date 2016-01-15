namespace RabbitMqNext.Internals
{
	using System.Collections.Concurrent;
	using System.Threading;
	using System.Threading.Tasks;

	/// <summary>
	/// The commmonality is how we handle with close messages started by the server.
	/// They either invalidate a connection or a channel
	/// </summary>
	public abstract class CommonCommandSender
	{
		internal AmqpError _lastError; // volative writes through other means

//		private volatile int _closed = 0;
		internal readonly ConcurrentQueue<CommandToSend> _awaitingReplyQueue;

		private volatile bool _closed;

		protected CommonCommandSender()
		{
			_awaitingReplyQueue = new ConcurrentQueue<CommandToSend>();
		}

		internal abstract Task HandleFrame(int classMethodId);

		protected void HandleCloseMethod(ushort replyCode, string replyText, ushort classId, ushort methodId)
		{
			var error = new AmqpError() { ClassId = classId, MethodId = methodId, ReplyCode = replyCode, ReplyText = replyText };
			Volatile.Write(ref _lastError, error);
			InitiateCleanClose(startedByServer: true, offendingClassId: classId, offendingMethodId: methodId);
//			DrainMethodsWithErrorAndClose(error, classId, methodId, sendCloseOk: true);
//			return Task.CompletedTask;
		}

		internal abstract void InitiateCleanClose(bool startedByServer, ushort offendingClassId, ushort offendingMethodId);

//		private override Task HandleCloseMethod(ushort channel, ushort replyCode, string replyText, ushort classId, ushort methodId)
//		{
//			var error = new AmqpError() {ClassId = classId, MethodId = methodId, ReplyCode = replyCode, ReplyText = replyText};
//			DrainMethodsWithErrorAndClose(error, classId, methodId, sendCloseOk: true);
//			return Task.CompletedTask;
//		}

		public Task Close()
		{
			Thread.MemoryBarrier();
			if (_closed) return Task.CompletedTask;
			_closed = true;

			InitiateCleanClose(startedByServer: false, offendingClassId: 0, offendingMethodId: 0);

			InternalDispose();

			return Task.CompletedTask;
		}

		public bool Closed { get { return _closed; } }

		internal abstract void InternalDispose();
	}
}