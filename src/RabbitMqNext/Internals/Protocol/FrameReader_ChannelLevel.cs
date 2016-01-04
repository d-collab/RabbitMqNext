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

		public async Task Read_Channel_Close2(Action<ushort, string, ushort, ushort> continuation)
		{
			var replyCode = await _amqpReader.ReadShort();
			var replyText = await _amqpReader.ReadShortStr();
			var classId = await _amqpReader.ReadShort();
			var methodId = await _amqpReader.ReadShort();

			Console.WriteLine("< channel close coz  " + replyText + " in class  " + classId + " methodif " + methodId);

			continuation(replyCode, replyText, classId, methodId);
		}

		public void Read_Channel_CloseOk(Action continuation)
		{
			Console.WriteLine("< channel close OK coz ");

			continuation();
		}
	}
}