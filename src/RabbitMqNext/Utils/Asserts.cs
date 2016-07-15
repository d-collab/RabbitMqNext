namespace RabbitMqNext.Utils
{
	using System.Diagnostics;
	using System.Runtime.CompilerServices;
	using Internals.RingBuffer;
	
	internal static class Asserts
	{
		[DebuggerStepThrough]
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void AssertNotReadFrameThread()
		{
#if ASSERT
			if (ThreadUtils.IsReadFrameThread())
			{
				LogAdapter.LogError("Asserts", "Assert failed: AssertNotReadFrameThread at " + new StackTrace());
			}
#endif
		}
	}

	
}
