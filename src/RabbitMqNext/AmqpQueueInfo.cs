namespace RabbitMqNext
{
	public class AmqpQueueInfo
	{
		public string Name { get; internal set; }
		public uint Messages { get; internal set; }
		public uint Consumers { get; internal set; }

		public override string ToString()
		{
			return "Queue: " + Name + "  Messages: " + Messages + "  Consumers: " + Consumers;
		}
	}
}