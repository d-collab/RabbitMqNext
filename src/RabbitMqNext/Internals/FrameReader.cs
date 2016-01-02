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

				Console.WriteLine("> channel " + channel + " payload " + payloadLength);

				if (frameType == AmqpConstants.FrameMethod)
				{
					ushort classId = await _reader.ReadUInt16();
					ushort methodId = await _reader.ReadUInt16();

					var classMethodId = classId << 16 | methodId;

					await _frameProcessor.DispatchMethod(channel, classMethodId);

//					Console.WriteLine(" received class " + classId + " method " + methodId + " id " + id);
//					switch(id)
//					{
//						case 655370: // ConnectionStart = 10 10
//							await Receive_ConnectionStart(channel, payloadLength);
//							break;
//
//						case 655371: 
//							await Receive_ConnectionStartOk(channel, payloadLength);
//							break;
//
//						case 655390: // Connection Tune
//							await Receive_ConnectionTune(channel, payloadLength);
//							break;
//
//						case 655410: // Connection close
//							await Receive_ConnectionClose(channel, payloadLength);
//							break;
//
//						case 655401: // Connection open ok
//							await Receive_ConnectionOpenOk(channel, payloadLength);
//							break;
//
//						default:
//							break;
//					}
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

			// this may cause a deadlock, need to test carefully
			// Task.Factory.StartNew(() => _frameProcessor._ConnectionStart(versionMajor, versionMinor, serverProperties, mechanisms, locales), TaskCreationOptions.PreferFairness);
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
	}
}