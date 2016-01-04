namespace RabbitMqNext.Internals
{
	using System;
	using System.Collections.Concurrent;
	using System.Threading.Tasks;

	internal static class Util
	{
		public static void DrainMethodsWithError(ConcurrentQueue<CommandToSend> awaitingReplyQueue, AmqpError amqpError, 
												 ushort classId, ushort methodId)
		{
			// releases every task awaiting
			CommandToSend sent;
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
		}

		public static void SetException<T>(TaskCompletionSource<T> tcs, AmqpError error, int classMethodId)
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