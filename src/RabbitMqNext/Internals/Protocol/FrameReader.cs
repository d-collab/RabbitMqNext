namespace RabbitMqNext.Internals
{
	using System;
	using System.Threading;
	using System.Threading.Tasks;

	internal partial class FrameReader
	{
		private readonly InternalBigEndianReader _reader;
		private readonly AmqpPrimitivesReader _amqpReader;
		private readonly IFrameProcessor _frameProcessor;

		public FrameReader(InternalBigEndianReader reader, 
						   AmqpPrimitivesReader amqpReader,
						   IFrameProcessor frameProcessor)
		{
			_reader = reader;
			_amqpReader = amqpReader;
			_frameProcessor = frameProcessor;
		}

		public void ReadAndDispatch()
		{
			try
			{
				byte frameType = _reader.ReadByte();

				if (frameType == 'A')
				{
					var msg = "Invalid frame received " + frameType;

					LogAdapter.LogError("FrameReader", msg);
					throw new Exception(msg);
				}

				ushort channel = _reader.ReadUInt16();
				int payloadLength = _reader.ReadInt32();

				if (LogAdapter.ProtocolLevelLogEnabled)
					LogAdapter.LogDebug("FrameReader", "> Incoming Frame (" + frameType + ") for channel [" + channel + "]  payload size: " + payloadLength);

				// needs special case for heartbeat, flow, etc.. 
				// since they are not replies to methods we sent and alter the client's behavior


				if (frameType == AmqpConstants.FrameMethod)
				{
					ushort classId = _reader.ReadUInt16();
					ushort methodId = _reader.ReadUInt16();

					var classMethodId = classId << 16 | methodId;

//					Console.WriteLine("> Incoming Method: class " + classId + " method " + methodId + " classMethodId " + classMethodId);

					_frameProcessor.DispatchMethod(channel, classMethodId);
				}
				else if (frameType == AmqpConstants.FrameHeartbeat)
				{
					if (LogAdapter.ProtocolLevelLogEnabled)
						LogAdapter.LogDebug("FrameReader", "Received FrameHeartbeat");
				}

				byte frameEndMarker = _reader.ReadByte();
				if (frameEndMarker != AmqpConstants.FrameEnd)
				{
					var msg = "Expecting frame end, but found " + frameEndMarker;

					LogAdapter.LogError("FrameReader", msg);
					throw new Exception(msg);
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