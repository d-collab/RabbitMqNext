namespace RabbitMqNext.Internals
{
	using System;
	using System.Threading;
	using System.Threading.Tasks;

	internal partial class FrameReader
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
//				Console.WriteLine("Frame type " + frameType);

				if (frameType == 'A')
				{
					// wtf
					Console.WriteLine("Meh, protocol header received for some reason. darn it!");
				}

				ushort channel = _reader.ReadUInt16();
				int payloadLength = await _reader.ReadInt32();

//				var initialPosition = _reader._ringBufferStream.Position;
//				Console.WriteLine("> Incoming Frame (" + frameType + ") for channel [" + channel + "]  payload size: " + payloadLength);

				// needs special case for heartbeat, flow, etc.. 
				// since they are not replies to methods we sent and alter the client's behavior

				ushort classId = 0;
				ushort methodId = 0;

				if (frameType == AmqpConstants.FrameMethod)
				{
					classId = _reader.ReadUInt16();
					methodId = _reader.ReadUInt16();

					var classMethodId = classId << 16 | methodId;

//					Console.WriteLine("> Incoming Method: class " + classId + " method " + methodId + " classMethodId " + classMethodId);

					if (classMethodId == AmqpClassMethodConnectionLevelConstants.ConnectionClose)
					{
						await Read_ConnectionClose2(async (replyCode, replyText, oClassId, oMethodId) =>
						{
							await _frameProcessor.DispatchCloseMethod(channel, replyCode, replyText, oClassId, oMethodId);
						});
					}
					else if (classMethodId == AmqpClassMethodChannelLevelConstants.ChannelClose)
					{
						await Read_Channel_Close2(async (replyCode, replyText, oClassId, oMethodId) =>
						{
							await _frameProcessor.DispatchChannelCloseMethod(channel, replyCode, replyText, oClassId, oMethodId);
						});
					}
					else
					{
						await _frameProcessor.DispatchMethod(channel, classMethodId);
					}
				}
//				else if (frameType == AmqpConstants.FrameHeader)
//				{
//					Console.WriteLine("received FrameHeader");
//				}
//				else if (frameType == AmqpConstants.FrameBody)
//				{
//					Console.WriteLine("received FrameBody");
//				}
				else if (frameType == AmqpConstants.FrameHeartbeat)
				{
					Console.WriteLine("received FrameHeartbeat");
				}

//				var curPos = _reader._ringBufferStream.Position;
//				var expectedPos = initialPosition + payloadLength;
//
//				if (expectedPos != curPos)
//				{
//					Console.WriteLine("Expecting stream pos to be at " + expectedPos + " but is at " + curPos + 
//						" for frame type " + frameType + " and class " + classId + " and method " + methodId);
//				}

//				Console.WriteLine("will read FrameEnd");
				int frameEndMarker = await _reader.ReadByte();
//				Console.WriteLine("done read FrameEnd " + frameEndMarker);
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
				Console.WriteLine("Frame Reader error: " + ex);
				throw;
			}
		}
	}
}