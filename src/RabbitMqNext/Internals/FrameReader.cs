namespace RabbitMqNext.Internals
{
	using System;
	using System.Collections.Generic;
	using System.Threading;
	using System.Threading.Tasks;


	internal class FrameReader
	{
		private readonly InternalBigEndianReader _reader;
		private readonly AmqpPrimitivesReader _amqpReader;
		private readonly FrameProcessor _frameProcessor;

		public FrameReader(InternalBigEndianReader reader, 
						   AmqpPrimitivesReader amqpReader,
						   FrameProcessor frameProcessor)
		{
			_reader = reader;
			_amqpReader = amqpReader;
			_frameProcessor = frameProcessor;
		}

		public async Task ReadAndDispatch()
		{
			try
			{
				var frameType = await _reader.ReadByte();

				Console.WriteLine("Frame type " + frameType);

				if (frameType == 'A')
				{
					// wtf
					Console.WriteLine("Meh, protocol header received for some reason. darn it!");
				}

				ushort channel = await _reader.ReadUInt16();
				int payloadLength = await _reader.ReadInt32();

				Console.WriteLine("> Incoming Frame: channel " + channel + " payload " + payloadLength);

				
				// needs special case for heartbeat, flow, etc.. 
				// since they are not replies to methods we sent and alter the client's behavior


				if (frameType == AmqpConstants.FrameMethod)
				{
					ushort classId = await _reader.ReadUInt16();
					ushort methodId = await _reader.ReadUInt16();

					var classMethodId = classId << 16 | methodId;

					Console.WriteLine("> Incoming Method: class " + classId + " method " + methodId + " classMethodId " + classMethodId);

					// needs special 

					if (classMethodId == AmqpClassMethodConstants.ConnectionClose)
					{
						await Read_ConnectionClose((replyCode, replyText, oClassId, oMethodId) =>
						{
							_frameProcessor.DispatchCloseMethod(channel, replyCode, replyText, oClassId, oMethodId);
						});
					}
					else
					{
						await _frameProcessor.DispatchMethod(channel, classMethodId);
					}
				}
				else if (frameType == AmqpConstants.FrameHeader)
				{

				}
				else if (frameType == AmqpConstants.FrameBody)
				{

				}
				else if (frameType == AmqpConstants.FrameHeartbeat)
				{

				}

				int frameEndMarker = await _reader.ReadByte();
				if (frameEndMarker != AmqpConstants.FrameEnd)
				{
					throw new Exception("Expecting frame end, but found " + frameEndMarker);
				}
			}
			catch (ThreadAbortException)
			{
				// no-op
			}
			catch (Exception ex)
			{
				Console.WriteLine("Handle error: " + ex);
				throw;
			}
		}

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

			Console.WriteLine("< conn open ok " + reserved );

			continuation(reserved);
		}

		public async Task Read_ConnectionTune(Action<ushort, uint, ushort> continuation)
		{
			var channelMax = await _amqpReader.ReadShort();
			var frameMax = await _amqpReader.ReadLong();
			var heartbeat = await _amqpReader.ReadShort();

			Console.WriteLine("< channelMax " + channelMax + " framemax " + frameMax + " hb " + heartbeat);

			continuation(channelMax, frameMax, heartbeat);
		}

		public async Task Read_ConnectionClose(Action<ushort, string, ushort, ushort> continuation)
		{
			var replyCode = await _amqpReader.ReadShort();
			var replyText = await _amqpReader.ReadShortStr();
			var classId = await _amqpReader.ReadShort();
			var methodId = await _amqpReader.ReadShort();

			Console.WriteLine("< close coz  " + replyText + " in class  " + classId + " methodif " + methodId);

			continuation(replyCode, replyText, classId, methodId);
		}

		public async Task Read_ChannelOpenOk(Action<string> continuation)
		{
			var reserved = await _amqpReader.ReadLongstr();

			Console.WriteLine("< ChannelOpenOk  " + reserved);

			continuation(reserved);
		}

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