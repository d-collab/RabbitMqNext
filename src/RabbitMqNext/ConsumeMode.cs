namespace RabbitMqNext
{
	public enum ConsumeMode
	{
		SingleThreaded,
		ParallelWithReadBarrier,
		ParallelWithBufferCopy
	}
}