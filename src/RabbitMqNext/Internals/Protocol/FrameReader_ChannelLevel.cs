namespace RabbitMqNext.Internals
{
	using System;
	using System.Threading.Tasks;

	internal partial class FrameReader
	{
		public void Read_BasicQosOk(Action continuation)
		{
			Console.WriteLine("< BasicQosOk  ");

			continuation();
		}

		public void Read_ExchangeDeclareOk(Action continuation)
		{
			Console.WriteLine("< ExchangeDeclareOk");

			continuation();
		}

		public async Task Read_QueueDeclareOk(Action<string, uint, uint> continuation)
		{
			Console.WriteLine("< QueueDeclareOk");

			var queue = await _amqpReader.ReadShortStr();
			var messageCount = await _amqpReader.ReadLong();
			var consumerCount = await _amqpReader.ReadLong();

			continuation(queue, messageCount, consumerCount);
		}

		public void Read_QueueBindOk(Action continuation)
		{
			Console.WriteLine("< QueueBindOk");

			continuation();
		}
	}
}