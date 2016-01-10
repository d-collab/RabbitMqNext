namespace RabbitMqNext.Internals
{
	using System;
	using System.Collections.Generic;
	using System.Threading.Tasks;

	internal partial class FrameReader
	{
		public void Read_ConnectionStart(Action<byte, byte, IDictionary<string, object>, string, string> continuation)
		{
			byte versionMajor = _amqpReader.ReadOctet();
			byte versionMinor = _amqpReader.ReadOctet();
			IDictionary<string,object> serverProperties = _amqpReader.ReadTable();
			string mechanisms = _amqpReader.ReadLongstr();
			string locales = _amqpReader.ReadLongstr();

			Console.WriteLine("< con_start " + mechanisms + " locales " + locales);

			continuation(versionMajor, versionMinor, serverProperties, mechanisms, locales);
		}

		public void Read_ConnectionOpenOk(Action<string> continuation)
		{
			string reserved = _amqpReader.ReadShortStr();

			Console.WriteLine("< conn open ok " + reserved);

			continuation(reserved);
		}

		public void Read_ConnectionTune(Action<ushort, uint, ushort> continuation)
		{
			ushort channelMax = _amqpReader.ReadShort();
			uint frameMax = _amqpReader.ReadLong();
			ushort heartbeat = _amqpReader.ReadShort();

			Console.WriteLine("< channelMax " + channelMax + " framemax " + frameMax + " hb " + heartbeat);

			continuation(channelMax, frameMax, heartbeat);
		}

		public Task Read_ConnectionClose2(Func<ushort, string, ushort, ushort, Task> continuation)
		{
			ushort replyCode = _amqpReader.ReadShort();
			string replyText = _amqpReader.ReadShortStr();
			ushort classId = _amqpReader.ReadShort();
			ushort methodId = _amqpReader.ReadShort();

			Console.WriteLine("< close coz  " + replyText + " in class  " + classId + " methodif " + methodId);

			return continuation(replyCode, replyText, classId, methodId);
		}

//		public void Read_ConnectionCloseOk(Action continuation)
//		{
//			Console.WriteLine("< ConnectionCloseOk  ");
//
//			continuation();
//		}

		public void Read_ChannelOpenOk(Action<string> continuation)
		{
			string reserved = _amqpReader.ReadLongstr();

			Console.WriteLine("< ChannelOpenOk  " + reserved);

			continuation(reserved);
		}
	}
}