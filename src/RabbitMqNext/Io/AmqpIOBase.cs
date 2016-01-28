namespace RabbitMqNext.Io
{
	using System;
	using System.Collections.Concurrent;
	using System.Threading;
	using System.Threading.Tasks;
	using RabbitMqNext;
	using RabbitMqNext.Internals;


	/// <summary>
	/// Commonality of connection and channel
	/// </summary>
	public abstract class AmqpIOBase : IDisposable
	{
		internal readonly ConcurrentQueue<CommandToSend> _awaitingReplyQueue;
		private volatile bool _isClosed, _isDisposed;
		internal readonly ushort _channelNum;

		// if not null, it's the error that closed the channel or connection
		internal AmqpError _lastError;

		protected AmqpIOBase(ushort channelNum)
		{
			_channelNum = channelNum;
			_awaitingReplyQueue = new ConcurrentQueue<CommandToSend>();
		}

		public Action<AmqpError> OnError;

		public bool IsClosed { get { return _isClosed; } }

		public async void Dispose()
		{
			if (_isDisposed) return;
			Thread.MemoryBarrier();
			_isDisposed = true;

			await InitiateCleanClose(false, null);

			InternalDispose();
		}

		protected abstract void InternalDispose();

		internal abstract Task HandleFrame(int classMethodId);

		internal abstract Task SendCloseConfirmation();

		internal abstract Task SendStartClose();

		// To be use in case of exceptions on our end. Close everything asap
		internal virtual void InitiateAbruptClose(Exception reason)
		{
			if (_isClosed) return;
			Thread.MemoryBarrier();
			_isClosed = true;

			var syntheticError = new AmqpError() {ReplyText = reason.Message};

			DrainPending(syntheticError);

			FireErrorEvent(syntheticError);

			this.Dispose();
		}

		internal virtual async Task<bool> InitiateCleanClose(bool initiatedByServer, AmqpError error)
		{
			if (_isClosed) return false;
			Thread.MemoryBarrier();
			_isClosed = true;

			if (initiatedByServer)
				await SendCloseConfirmation();
			else
				await SendStartClose();

			DrainPending(error);

			FireErrorEvent(error);

			return true;
		}

		internal Task HandReplyToAwaitingQueue(int classMethodId)
		{
			CommandToSend sent;

			if (_awaitingReplyQueue.TryDequeue(out sent))
			{
				return sent.RunReplyAction(_channelNum, classMethodId, null);
			}
			// else
			{
				// nothing was really waiting for a reply.. exception? wtf?
				// TODO: log
			}

			return Task.CompletedTask;
		}

		// A disconnect may be expected coz we send a connection close, etc.. 
		// or it may be something abruptal
		internal void HandleDisconnect()
		{
			if (_isClosed) return; // we have initiated the close

			// otherwise

			_lastError = new AmqpError { ClassId = 0, MethodId = 0, ReplyCode = 0, ReplyText = "disconnected" };

			DrainPending(_lastError);
		}

		internal Task<bool> HandleCloseMethodFromServer(AmqpError error) 
		{
			_lastError = error;
			return InitiateCleanClose(true, error);
		}

		private void DrainPending(AmqpError error)
		{
			// releases every task awaiting
			CommandToSend sent;
#pragma warning disable 4014
			while (_awaitingReplyQueue.TryDequeue(out sent))
			{
				if (error != null && sent.ClassId == error.ClassId && sent.MethodId == error.MethodId)
				{
					// if we find the "offending" command, then it gets a better error message
					sent.RunReplyAction(0, 0, error);
				}
				else
				{
					// any other task dies with a generic error.
					sent.RunReplyAction(0, 0, null);
				}
			}
#pragma warning restore 4014
		}

		private void FireErrorEvent(AmqpError error)
		{
			var ev = this.OnError;
			if (ev != null && error != null)
			{
				ev(error);
			}
		}

		internal static void SetException<T>(TaskCompletionSource<T> tcs, AmqpError error, int classMethodId)
		{
			if (tcs == null) return;
			if (error != null)
			{
				tcs.SetException(new Exception("Error: " + error.ToErrorString()));
			}
			else if (classMethodId == 0)
			{
				tcs.SetException(new Exception("The server closed the connection"));
			}
			else
			{
				var classId = classMethodId >> 16;
				var methodId = classMethodId & 0x0000FFFF;

				Console.WriteLine("Unexpected situation: classId = " + classId + " method " + methodId + " and error = null");
				tcs.SetException(new Exception("Unexpected reply from the server: classId = " + classId + " method " + methodId));
			}
		}

		internal static void SetException(TaskSlim tcs, AmqpError error, int classMethodId)
		{
			if (tcs == null) return;
			if (error != null)
			{
				tcs.SetException(new Exception("Error: " + error.ToErrorString()));
			}
			else if (classMethodId == 0)
			{
				tcs.SetException(new Exception("The server closed the connection"));
			}
			else
			{
				var classId = classMethodId >> 16;
				var methodId = classMethodId & 0x0000FFFF;

				Console.WriteLine("Unexpected situation: classId = " + classId + " method " + methodId + " and error = null");
				tcs.SetException(new Exception("Unexpected reply from the server: classId = " + classId + " method " + methodId));
			}
		}
	}
}