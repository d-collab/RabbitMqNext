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

			if (LogAdapter.ProtocolLevelLogEnabled)
				LogAdapter.LogDebug(LogSource, "< ConnectionStart " + mechanisms + " " + locales);

			continuation(versionMajor, versionMinor, serverProperties, mechanisms, locales);
		}

		public void Read_ConnectionOpenOk(Action<string> continuation)
		{
			string reserved = _amqpReader.ReadShortStr();

			if (LogAdapter.ProtocolLevelLogEnabled)
				LogAdapter.LogDebug(LogSource, "< ConnectionOpenOk " + reserved);

			continuation(reserved);
		}

		public void Read_ConnectionTune(Action<ushort, uint, ushort> continuation)
		{
			ushort channelMax = _amqpReader.ReadShort();
			uint frameMax = _amqpReader.ReadLong();
			ushort heartbeat = _amqpReader.ReadShort();

			if (LogAdapter.ProtocolLevelLogEnabled)
				LogAdapter.LogDebug(LogSource, "< ConnectionTune: ChannelMax " + channelMax + " FrameMax " + frameMax + " Heartbeat " + heartbeat);

			continuation(channelMax, frameMax, heartbeat);
		}

		public async void Read_ConnectionClose(Func<AmqpError, Task<bool>> continuation)
		{
			ushort replyCode = _amqpReader.ReadShort();
			string replyText = _amqpReader.ReadShortStr();
			ushort classId = _amqpReader.ReadShort();
			ushort methodId = _amqpReader.ReadShort();

			if (LogAdapter.ProtocolLevelLogEnabled)
				LogAdapter.LogDebug(LogSource, "< ConnectionClose " + replyText + " in class  " + classId + " method " + methodId);

			await continuation(new AmqpError { ClassId = classId, MethodId = methodId, ReplyText = replyText, ReplyCode = replyCode }).ConfigureAwait(false);
		}

		public void Read_ChannelOpenOk(Action<string> continuation)
		{
			string reserved = _amqpReader.ReadLongstr();

			if (LogAdapter.ProtocolLevelLogEnabled)
				LogAdapter.LogDebug(LogSource, "< ChannelOpenOk " + reserved);

			continuation(reserved);
		}

		public void Read_ConnectionBlocked(Action<string> continuation)
		{
			string reason = _amqpReader.ReadShortStr();

			if (LogAdapter.ProtocolLevelLogEnabled)
				LogAdapter.LogDebug(LogSource, "< ConnectionBlocked " + reason);

			continuation(reason);
		}
	}
}