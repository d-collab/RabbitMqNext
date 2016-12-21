namespace RabbitMqNext.Internals
{
	using System;
	using System.Threading;
	using System.Threading.Tasks;

	internal partial class FrameReader
	{
		private InternalBigEndianReader _reader;
		private AmqpPrimitivesReader _amqpReader;
		private IFrameProcessor _frameProcessor;


		public void Initialize(InternalBigEndianReader reader, 
							   AmqpPrimitivesReader amqpReader, IFrameProcessor frameProcessor)
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

					LogAdapter.LogError(LogSource, msg);
					throw new Exception(msg);
				}

				ushort channel = _reader.ReadUInt16();
				int payloadLength = _reader.ReadInt32();

				// needs special case for heartbeat, flow, etc.. 
				// since they are not replies to methods we sent and alter the client's behavior

				ushort classId, methodId;
				classId = methodId = 0;
				if (frameType == AmqpConstants.FrameMethod)
				{
					classId = _reader.ReadUInt16();
					methodId = _reader.ReadUInt16();

					var classMethodId = classId << 16 | methodId;

					if (LogAdapter.ProtocolLevelLogEnabled)
						LogAdapter.LogDebug(LogSource, "> Incoming Frame (" + frameType + ") for channel [" + channel + "] class (" + classId + ") method  (" + methodId + ") payload size: " + payloadLength);

					_frameProcessor.DispatchMethod(channel, classMethodId);
				}
				else if (frameType == AmqpConstants.FrameHeartbeat)
				{
					if (LogAdapter.ProtocolLevelLogEnabled)
						LogAdapter.LogDebug(LogSource, "Received FrameHeartbeat");

					_frameProcessor.DispatchHeartbeat();
				}

				byte frameEndMarker = _reader.ReadByte();
				if (frameEndMarker != AmqpConstants.FrameEnd)
				{
					var msg = "Expecting frame end, but found " + frameEndMarker + ". The original class and method: " + classId + " " + methodId;

					LogAdapter.LogError(LogSource, msg);
					throw new Exception(msg);
				}
			}
			catch (ThreadAbortException)
			{
				// no-op
			}
			catch (Exception ex)
			{
				if (LogAdapter.IsErrorEnabled) LogAdapter.LogError(LogSource, "Frame Reader error", ex);
				throw;
			}
		}
	}
}