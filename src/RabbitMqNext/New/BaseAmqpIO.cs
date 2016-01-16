namespace RabbitMqNext
{
	using System;
	using System.Collections.Concurrent;
	using System.Threading;
	using System.Threading.Tasks;
	using Internals;



	public abstract class BaseAmqpIO : IDisposable
	{
		private volatile bool _closed;

		internal readonly ConcurrentQueue<CommandToSend> _awaitingReplyQueue;
		internal AmqpError _lastError; // volative writes through other means

		protected BaseAmqpIO()
		{
			_awaitingReplyQueue = new ConcurrentQueue<CommandToSend>();
		}

		public void Dispose()
		{
			InternalDispose();
		}

		internal abstract Task HandleFrame(int classMethodId);

		internal abstract void InitiateCleanClose(bool initiatedByServer, ushort offendingClassId, ushort offendingMethodId);

		internal abstract void InternalDispose();

		internal void HandleCloseMethodFromServer(ushort replyCode, string replyText, ushort classId, ushort methodId)
		{
			var error = new AmqpError() { ClassId = classId, MethodId = methodId, ReplyCode = replyCode, ReplyText = replyText };
			// DrainMethodsWithErrorAndClose(error, classId, methodId, sendCloseOk: true);
			// return Task.CompletedTask;
		}

		internal void HandleDisconnect()
		{
			// A disconnect may be expected coz we send a connection close, etc.. 
			// or it may be something abruptal, 
		}

		internal Task HandReplyToAwaitingQueue(int classMethodId)
		{
			CommandToSend sent;

			if (_awaitingReplyQueue.TryDequeue(out sent))
			{
				return sent.ReplyAction3(0, classMethodId, null);
			}
			// else
			{
				// nothing was really waiting for a reply.. exception? wtf?
				// TODO: log
			}

			return Task.CompletedTask;
		}

		private bool Closed { get { return _closed; } }

		private Task DoClose() // initiated by the user, not the server
		{
			if (_closed) return Task.CompletedTask;
			Thread.MemoryBarrier();
			_closed = true;

			InitiateCleanClose(false, 0, 0);

			return Task.CompletedTask;
		}

		private static void DrainMethodsWithError(ConcurrentQueue<CommandToSend> awaitingReplyQueue, AmqpError amqpError,
												 ushort classId, ushort methodId)
		{
			// releases every task awaiting
			CommandToSend sent;
#pragma warning disable 4014
			while (awaitingReplyQueue.TryDequeue(out sent))
			{
				if (/*sent.Channel == channel &&*/ sent.ClassId == classId && sent.MethodId == methodId)
				{
					// if we find the "offending" command, then it gets a better error message
					sent.ReplyAction3(0, -1, amqpError);
				}
				else
				{
					// any other task dies with a generic error.
					sent.ReplyAction3(0, -1, null);
				}
			}
#pragma warning restore 4014
		}

		internal static void SetException<T>(TaskCompletionSource<T> tcs, AmqpError error, int classMethodId)
		{
			if (error != null)
				tcs.SetException(new Exception("Error: " + error.ToErrorString()));
			else if (classMethodId == -1)
				tcs.SetException(new Exception("The server closed the connection"));
			else
			{
				Console.WriteLine("Unexpected situation: classMethodId = " + classMethodId + " and error = null");
				tcs.SetException(new Exception("Unexpected reply from the server: " + classMethodId));
			}
		}
	}
}