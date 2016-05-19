namespace RabbitMqNext
{
	public enum ConsumeMode
	{
		/// <summary>
		/// Same client thread that read the content of the message is used to deliver (invoke user's code). 
		/// Less overhead but less robust. Ordering is guaranted. 
		/// </summary>
		SingleThreaded,

//		ParallelWithReadBarrier,

		/// <summary>
		/// The new message is posted as a new Task, and it's up the the TaskScheduler to 
		/// call the user code. More robust, but involved buffer copy and properties cloning. 
		/// Also, there are no guarantes regarding ordering. 
		/// </summary>
		ParallelWithBufferCopy,

		/// <summary>
		/// Same as <see cref="ParallelWithBufferCopy"/> but with ordering guaranted.
		/// </summary>
		SerializedWithBufferCopy,
	}
}