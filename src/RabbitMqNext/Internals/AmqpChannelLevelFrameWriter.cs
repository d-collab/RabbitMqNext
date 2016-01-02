namespace RabbitMqNext.Internals
{
	using System;

	static class AmqpChannelLevelFrameWriter
	{
		public static Action<AmqpPrimitivesWriter> ExchangeDeclare()
		{
			return writer =>
			{

			};
		}

		public static Action<AmqpPrimitivesWriter> BasicPublish()
		{
			return writer =>
			{

			};
		}
	}
}