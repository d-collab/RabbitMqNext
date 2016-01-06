namespace RabbitMqNext.Internals
{
	using System;
	using System.Collections.Generic;
	using System.Threading.Tasks;

	internal partial class FrameReader
	{
		public async Task Read_ConnectionStart(Action<byte, byte, IDictionary<string, object>, string, string> continuation)
		{
			var versionMajor = await _amqpReader.ReadOctet();
			var versionMinor = await _amqpReader.ReadOctet();
			var serverProperties = await _amqpReader.ReadTable();
			var mechanisms = await _amqpReader.ReadLongstr();
			var locales = await _amqpReader.ReadLongstr();

			Console.WriteLine("< con_start " + mechanisms + " locales " + locales);

			continuation(versionMajor, versionMinor, serverProperties, mechanisms, locales);
		}

		public async Task Read_ConnectionOpenOk(Action<string> continuation)
		{
			var reserved = await _amqpReader.ReadShortStr();

			Console.WriteLine("< conn open ok " + reserved);

			continuation(reserved);
		}

		public async Task Read_ConnectionTune(Action<ushort, uint, ushort> continuation)
		{
			ushort channelMax = _amqpReader.ReadShort();
			uint frameMax = _amqpReader.ReadLong();
			ushort heartbeat = _amqpReader.ReadShort();

			Console.WriteLine("< channelMax " + channelMax + " framemax " + frameMax + " hb " + heartbeat);

			continuation(channelMax, frameMax, heartbeat);
		}

		public async Task Read_ConnectionClose2(Action<ushort, string, ushort, ushort> continuation)
		{
			var replyCode =  _amqpReader.ReadShort();
			var replyText = await _amqpReader.ReadShortStr();
			var classId =  _amqpReader.ReadShort();
			var methodId =  _amqpReader.ReadShort();

			Console.WriteLine("< close coz  " + replyText + " in class  " + classId + " methodif " + methodId);

			continuation(replyCode, replyText, classId, methodId);
		}

		public void Read_ConnectionCloseOk(Action continuation)
		{
			Console.WriteLine("< ConnectionCloseOk  ");

			continuation();
		}

		public async Task Read_ChannelOpenOk(Action<string> continuation)
		{
			var reserved = await _amqpReader.ReadLongstr();

			Console.WriteLine("< ChannelOpenOk  " + reserved);

			continuation(reserved);
		}
	}
}