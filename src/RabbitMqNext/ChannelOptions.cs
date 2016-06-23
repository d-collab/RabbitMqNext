namespace RabbitMqNext
{
	using System.Threading.Tasks;

	public class ChannelOptions
	{
		/// <summary>
		/// Optional scheduler that will be used when 
		/// consuming with <see cref="ConsumeMode.ParallelWithBufferCopy"/>
		/// </summary>
		public TaskScheduler Scheduler { get; set; }
	}
}