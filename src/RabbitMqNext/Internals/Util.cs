namespace RabbitMqNext.Internals
{
	using System;
	using System.Threading.Tasks;

	internal static class Util
	{
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