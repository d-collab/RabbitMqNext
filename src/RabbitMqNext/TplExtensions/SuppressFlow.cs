namespace RabbitMqNext.TplExtensions
{
	using System;
	using System.Threading;

	public class SuppressFlow : IDisposable
	{
		private readonly AsyncFlowControl _undoer;

		public SuppressFlow()
		{
			_undoer = ExecutionContext.SuppressFlow();
		}

		public void Dispose()
		{
			_undoer.Undo();
		}
	}
}