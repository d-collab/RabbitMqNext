namespace RabbitMqNext
{
	using System;
	using System.Diagnostics;

	/// <summary>
	/// This is a hack to not carry a dependency on a particular logging library.
	/// Set the delegates to bridge to your favorite 
	/// logging library and set ExtendedLogEnabled to true. 
	/// </summary>
	public static class LogAdapter
	{
		public static bool ExtendedLogEnabled;
		public static bool ProtocolLevelLogEnabled;

		public static bool IsDebugEnabled;
		public static bool IsWarningEnabled;
		public static bool IsErrorEnabled;

		// signature: class, message, [exception]
		public static Action<string, string, Exception> LogDebugFn { get; set; }
		public static Action<string, string, Exception> LogErrorFn { get; set; }
		public static Action<string, string, Exception> LogWarnFn { get; set; }

		static LogAdapter()
		{
			IsDebugEnabled = IsWarningEnabled = IsErrorEnabled = true;


			LogDebugFn = (c, m, ex) =>
			{
				var msg = string.Format("Debug [{0}]: {1} {2}", c, m, ex);
				Console.Out.WriteLine(msg);
				if (Debugger.IsLogging())
				{
					Debugger.Log(5, "DEBUG", msg);
				}
			};

			LogErrorFn = (c, m, ex) =>
			{
				var msg = string.Format("Error [{0}]: {1} {2}", c, m, ex);
				Console.Error.WriteLine(msg);
				if (Debugger.IsLogging())
				{
					Debugger.Log(1, "ERROR", msg);
				}
			};

			LogWarnFn = (c, m, ex) =>
			{
				var msg = string.Format("Warn  [{0}]: {1} {2}", c, m, ex);
				Console.Out.WriteLine(msg);
				if (Debugger.IsLogging())
				{
					Debugger.Log(3, "WARN", msg);
				}
			};
		}


		public static void LogDebug(string context, string message)
		{
			LogDebugFn(context, message, null);
		}

		public static void LogDebug(string context, string message, Exception ex)
		{
			LogDebugFn(context, message, ex);
		}

		public static void LogError(string context, string message)
		{
			LogErrorFn(context, message, null);
		}

		public static void LogError(string context, string message, Exception ex)
		{
			LogErrorFn(context, message, ex);
		}

		public static void LogWarn(string context, string message, Exception ex = null)
		{
			LogWarnFn(context, message, ex);
		}
	}
}