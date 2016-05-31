namespace RabbitMqNext
{
	using System;
	using System.Runtime.CompilerServices;
	using TplExtensions;

//	public struct ValueTaskSlim<T> : BaseTaskSlim<ValueTaskSlim<T>>, INotifyCompletion where T : struct
//	{
//		private T _result;
//
//		internal long Started; // for timeout handling
//		internal uint Id;  // for correlation checking 
//
//		public ValueTaskSlim(Action<TaskSlim<T>> recycler) : base(recycler)
//		{
//		}
//
//		public void OnCompleted(Action continuation)
//		{
//			SetContinuation(continuation);
//		}
//
//		public TaskSlim<T> GetAwaiter()
//		{
//			return this;
//		}
//
//		public T GetResult()
//		{
//			if (HasException) throw _exception;
//
//			// will only be called by Compiler generated code if IsCompleted = true
//			return _result; 
//		}
//
//		public void SetResult(T result, bool runContinuationAsync = false)
//		{
//			_result = result;
//
//			SetCompleted(runContinuationAsync);
//		}
//
//		public override void Recycle()
//		{
//			base.Recycle();
//			_result = default(T);
//		}
//	}
}