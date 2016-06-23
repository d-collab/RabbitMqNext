namespace RabbitMqNext.Utils
{
	using System;
	using System.Diagnostics;
	using System.Runtime.CompilerServices;
	using Io;

	internal static class Asserts
	{
		[DebuggerStepThrough]
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void AssertNotReadFrameThread()
		{
#if ASSERT
			var name = System.Threading.Thread.CurrentThread.Name;
			if (name != null && name.StartsWith(ConnectionIO.ReadFrameThreadNamePrefix, StringComparison.Ordinal))
			{
				LogAdapter.LogError("Asserts", "Assert failed: AssertNotReadFrameThread at " + new StackTrace());
			}
#endif
		}
	}
}
