namespace RabbitMqNext.Internals
{
	using System;
	using System.Runtime.CompilerServices;
	using System.Threading.Tasks;


	internal partial class FrameReader
	{
		private const string LogSource = "FrameReader";

		public async Task Read_QueueDeclareOk(Func<string, uint, uint, Task> continuation)
		{
			string queue = _amqpReader.ReadShortStr();
			uint messageCount = _amqpReader.ReadLong();
			uint consumerCount = _amqpReader.ReadLong();

			if (LogAdapter.ProtocolLevelLogEnabled)
				LogAdapter.LogDebug(LogSource, "< QueueDeclareOk " + queue);

			await continuation(queue, messageCount, consumerCount).ConfigureAwait(false);
		}

		public async void Read_Channel_Close2(Func<AmqpError, Task<bool>> continuation)
		{
			ushort replyCode = _amqpReader.ReadShort();
			string replyText = _amqpReader.ReadShortStr();
			ushort classId = _amqpReader.ReadShort();
			ushort methodId = _amqpReader.ReadShort();

			if (LogAdapter.ProtocolLevelLogEnabled)
				LogAdapter.LogDebug(LogSource, "< ChannelClose " + replyText + " in class  " + classId + " method " + methodId);

			await continuation(new AmqpError() { ClassId = classId, MethodId = methodId, ReplyText = replyText, ReplyCode = replyCode }).ConfigureAwait(false);
		}

		public async Task Read_BasicDelivery(
			// Func<string, ulong, bool, string, string, int, BasicProperties, BaseLightStream, Task> continuation, 
			Channel channelImpl,
			BasicProperties properties)
		{
			string consumerTag = _amqpReader.ReadShortStr();
			ulong deliveryTag = _amqpReader.ReadULong();
			bool redelivered = _amqpReader.ReadBits() != 0;
			string exchange =  _amqpReader.ReadShortStr();
			string routingKey =  _amqpReader.ReadShortStr();

			byte frameEndMarker = _amqpReader.ReadOctet();
			if (frameEndMarker != AmqpConstants.FrameEnd) throw new Exception("Expecting frameend!");

			// Frame Header / Content header

			byte frameHeaderStart = _amqpReader.ReadOctet();
			if (frameHeaderStart != AmqpConstants.FrameHeader) throw new Exception("Expecting Frame Header");

			// await _reader.SkipBy(4 + 2 + 2 + 2);
			ushort channel = _reader.ReadUInt16();
			int payloadLength = _reader.ReadInt32();
			ushort classId = _reader.ReadUInt16();
			ushort weight = _reader.ReadUInt16();
			long bodySize = (long) _reader.ReadUInt64();

			// BasicProperties properties = ReadRestOfContentHeader();
			ReadRestOfContentHeader(properties, bodySize == 0);

			// Frame Body(s)

			if (bodySize != 0)
			{
				// Support just single body at this moment.

				frameHeaderStart = _reader.ReadByte();
				if (frameHeaderStart != AmqpConstants.FrameBody)
				{
					LogAdapter.LogError(LogSource, "Expecting FrameBody but got " + frameHeaderStart);

					throw new Exception("Expecting Frame Body");
				}

				// await _reader.SkipBy(2);
				channel = _reader.ReadUInt16();
				uint length = _reader.ReadUInt32();

				// Pending Frame end

				if (length == bodySize)
				{
					// TODO: Experimenting in making sure the body is available ...
					// TODO: ... before invoking the callback so we block this IO thread only

					// _reader._ringBufferStream.EnsureAvailableToRead(bodySize);

					await channelImpl.DispatchDeliveredMessage(consumerTag, deliveryTag, redelivered, exchange,
						routingKey, (int)length, properties, _reader._ringBufferStream).ConfigureAwait(false);

//					await continuation(consumerTag, deliveryTag, redelivered, exchange,
//					    routingKey, (int) length, properties, _reader._ringBufferStream).ConfigureAwait(false);
				}
				else
				{
					await channelImpl.DispatchDeliveredMessage(consumerTag, deliveryTag, redelivered, exchange,
						routingKey, (int)bodySize, properties, new MultiBodyStreamWrapper(_reader._ringBufferStream, (int)length, bodySize)).ConfigureAwait(false);
				}
			}
			else
			{
				// Empty body size

				await channelImpl.DispatchDeliveredMessage(consumerTag, deliveryTag, redelivered, exchange, routingKey, 0, properties, null).ConfigureAwait(false);
			}
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		private void ReadRestOfContentHeader(BasicProperties properties, bool skipFrameEnd)
		{
			var presence = _reader.ReadUInt16();

			if (presence != 0) // no header content
			{
				properties._presenceSWord = presence;
				if (properties.IsContentTypePresent) { properties.ContentType =  _amqpReader.ReadShortStr(); }
				if (properties.IsContentEncodingPresent) { properties.ContentEncoding =  _amqpReader.ReadShortStr(); }
				if (properties.IsHeadersPresent) { _amqpReader.ReadTable(properties.Headers); }
				if (properties.IsDeliveryModePresent) { properties.DeliveryMode = _amqpReader.ReadOctet(); }
				if (properties.IsPriorityPresent) { properties.Priority = _amqpReader.ReadOctet(); }
				if (properties.IsCorrelationIdPresent) { properties.CorrelationId =  _amqpReader.ReadShortStr(); }
				if (properties.IsReplyToPresent) { properties.ReplyTo =  _amqpReader.ReadShortStr(); }
				if (properties.IsExpirationPresent) { properties.Expiration =  _amqpReader.ReadShortStr(); }
				if (properties.IsMessageIdPresent) { properties.MessageId =  _amqpReader.ReadShortStr(); }
				if (properties.IsTimestampPresent) { properties.Timestamp =  _amqpReader.ReadTimestamp(); }
				if (properties.IsTypePresent) { properties.Type =  _amqpReader.ReadShortStr(); }
				if (properties.IsUserIdPresent) { properties.UserId =  _amqpReader.ReadShortStr(); }
				if (properties.IsAppIdPresent) { properties.AppId =  _amqpReader.ReadShortStr(); }
				if (properties.IsClusterIdPresent) { properties.ClusterId = _amqpReader.ReadShortStr(); }
			}

			if (!skipFrameEnd)
			{
				byte frameEndMarker = _reader.ReadByte();
				if (frameEndMarker != AmqpConstants.FrameEnd)
				{
					LogAdapter.LogError(LogSource, "Expecting FrameEnd but got " + frameEndMarker);

					throw new Exception("Expecting frameend");
				}
			}
		}

		public void Read_BasicConsumeOk(Action<string> continuation)
		{
			var consumerTag = _amqpReader.ReadShortStr();

			// Console.WriteLine("< BasicConsumeOk  " + consumerTag);

			continuation(consumerTag);
		}

		public async Task Read_BasicReturn(Channel channelImpl,
			// Func<ushort, string, string, string, int, BasicProperties, BaseLightStream, Task> continuation, 
			BasicProperties properties)
		{
			ushort replyCode = _amqpReader.ReadShort();
			string replyText = _amqpReader.ReadShortStr();
			string exchange = _amqpReader.ReadShortStr();
			string routingKey = _amqpReader.ReadShortStr();

			byte frameEndMarker = _amqpReader.ReadOctet();
			if (frameEndMarker != AmqpConstants.FrameEnd)
			{
				LogAdapter.LogError(LogSource, "Expecting FrameEnd but got " + frameEndMarker);

				throw new Exception("Expecting frameend!");
			}

			// Frame Header / Content header

			byte frameHeaderStart = _amqpReader.ReadOctet();
			if (frameHeaderStart != AmqpConstants.FrameHeader)
			{
				LogAdapter.LogError(LogSource, "Expecting FrameHeader but got " + frameHeaderStart);

				throw new Exception("Expecting Frame Header");
			}

			// await _reader.SkipBy(4 + 2 + 2 + 2);
			ushort channel = _reader.ReadUInt16();
			int payloadLength = _reader.ReadInt32();
			ushort classId = _reader.ReadUInt16();
			ushort weight = _reader.ReadUInt16();
			var bodySize = (long) _reader.ReadUInt64();

			// BasicProperties properties = ReadRestOfContentHeader();
			ReadRestOfContentHeader(properties, bodySize == 0);

			// Frame Body(s)
			if (bodySize != 0)
			{
				frameHeaderStart = _reader.ReadByte();
				if (frameHeaderStart != AmqpConstants.FrameBody)
				{
					LogAdapter.LogError(LogSource, "Expecting FrameBody but got " + frameHeaderStart);
					throw new Exception("Expecting Frame Body");
				}

				await _reader.SkipBy(2).ConfigureAwait(false); // channel = _reader.ReadUInt16();
				uint length = _reader.ReadUInt32();

				// must leave pending Frame end

				if (length == bodySize)
				{
					// TODO: Experimenting in making sure the body is available ...
					// TODO: ... before invoking the callback so we block this IO thread only

					// _reader._ringBufferStream.EnsureAvailableToRead(bodySize);

					await channelImpl.DispatchBasicReturn(replyCode, replyText, exchange,
						routingKey, (int)length, properties, _reader._ringBufferStream).ConfigureAwait(false);
				}
				else
				{
					await channelImpl.DispatchBasicReturn(replyCode, replyText, exchange,
						routingKey, (int)bodySize, properties, new MultiBodyStreamWrapper(_reader._ringBufferStream, (int)length, bodySize)).ConfigureAwait(false);
				}
			}
			else
			{
				// no body

				await channelImpl.DispatchBasicReturn(replyCode, replyText, exchange, routingKey, 0, properties, null).ConfigureAwait(false);
			}
		}

		public void Read_BasicAck(Channel channel)
		{
			ulong deliveryTags = _amqpReader.ReadULong();
			bool multiple = _amqpReader.ReadBits() != 0;

			if (LogAdapter.ProtocolLevelLogEnabled)
				LogAdapter.LogError(LogSource, "< BasicAck : " + deliveryTags + " multiple " + multiple);

			channel.ProcessAcks(deliveryTags, multiple);
		}

		public void Read_BasicNAck(Channel channel)
		{
			ulong deliveryTags = _amqpReader.ReadULong();
			byte bits = _amqpReader.ReadBits();
			bool multiple = (bits & 1) != 0;
			bool requeue = (bits & 2) != 0;

			if (LogAdapter.ProtocolLevelLogEnabled)
				LogAdapter.LogError(LogSource, "< BasicNAck from server for  " + deliveryTags + " multiple:  " + multiple + " requeue " + requeue);

			channel.ProcessNAcks(deliveryTags, multiple, requeue);
		}

		public void Read_ChannelFlow(Action<bool> continuation)
		{
			bool isActive = _amqpReader.ReadBits() != 0;

			LogAdapter.LogWarn(LogSource, "< ChannelFlow " + isActive);

			continuation(isActive);
		}

		public void Read_CancelOk(Action<string> continuation)
		{
			var consumerTag = _amqpReader.ReadShortStr();

			if (LogAdapter.ProtocolLevelLogEnabled)
				LogAdapter.LogDebug(LogSource, "< CancelOk " + consumerTag);

			continuation(consumerTag);
		}

		public Task Read_GenericMessageCount(Func<uint, Task> continuation)
		{
			uint messageCount = _amqpReader.ReadLong();

			if (LogAdapter.ProtocolLevelLogEnabled)
				LogAdapter.LogError(LogSource, "< GenericMessageCount : " + messageCount);

			return continuation(messageCount);
		}

		public Task Read_BasicCancel(Func<string, byte, Task> continuation)
		{
			string consumerTag = _amqpReader.ReadShortStr();
			var noWait = _amqpReader.ReadBits();

			if (LogAdapter.ProtocolLevelLogEnabled)
				LogAdapter.LogError(LogSource, "< BasicCancel : " + consumerTag + " bits " + noWait);

			return continuation(consumerTag, noWait);
		}
	}
}