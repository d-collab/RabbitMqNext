namespace RabbitMqNext.Internals
{
	using System;
	using System.IO;
	using System.Threading.Tasks;
	using RingBuffer;

	internal partial class FrameReader
	{
		public async Task Read_QueueDeclareOk(Func<string, uint, uint, Task> continuation)
		{
			Console.WriteLine("< QueueDeclareOk");

			string queue = await _amqpReader.ReadShortStr();
			uint messageCount = _amqpReader.ReadLong();
			uint consumerCount = _amqpReader.ReadLong();

			await continuation(queue, messageCount, consumerCount);
		}

		public async Task Read_Channel_Close2(Func<ushort, string, ushort, ushort, Task> continuation)
		{
			var replyCode = _amqpReader.ReadShort();
			var replyText = await _amqpReader.ReadShortStr();
			var classId = _amqpReader.ReadShort();
			var methodId = _amqpReader.ReadShort();

			Console.WriteLine("< channel close coz  " + replyText + " in class  " + classId + " methodif " + methodId);

			await continuation(replyCode, replyText, classId, methodId);
		}

		public async Task Read_BasicDelivery(Func<string, ulong, bool, string, string, long, BasicProperties, Stream, Task> continuation)
		{
			var consumerTag = await _amqpReader.ReadShortStr();
			var deliveryTag = await _amqpReader.ReadULong();
			var redelivered = await _amqpReader.ReadBits() != 0;
			var exchange = await _amqpReader.ReadShortStr();
			var routingKey = await _amqpReader.ReadShortStr();

			var frameEndMarker = await _amqpReader.ReadOctet();
			if (frameEndMarker != AmqpConstants.FrameEnd) throw new Exception("Expecting frameend!");

			// Frame Header / Content header

			var frameHeaderStart = await _amqpReader.ReadOctet();
			if (frameHeaderStart != AmqpConstants.FrameHeader) throw new Exception("Expecting Frame Header");

			// await _reader.SkipBy(4 + 2 + 2 + 2);
			ushort channel = _reader.ReadUInt16();
			int payloadLength = await _reader.ReadInt32();
			var classId = _reader.ReadUInt16();
			var weight = _reader.ReadUInt16();
			var bodySize = (long) await _reader.ReadUInt64();

			var properties = await ReadRestOfContentHeader();

			// Frame Body(s)

			// Support just single body at this moment.

			frameHeaderStart = await _reader.ReadByte();
			if (frameHeaderStart != AmqpConstants.FrameBody) throw new Exception("Expecting Frame Body");

			// await _reader.SkipBy(2);
			channel =   _reader.ReadUInt16();
			uint length = _reader.ReadUInt32();

			// Pending Frame end

			if (length == bodySize)
			{
				var marker = new RingBufferPositionMarker(_reader._ringBufferStream._ringBuffer);

				await continuation(consumerTag, deliveryTag, redelivered, exchange,
					routingKey, length, properties, (Stream) _reader._ringBufferStream);

				if (marker.LengthRead < length)
				{
					checked
					{
						int offset = (int) ( length - marker.LengthRead );
						await _reader.SkipBy(offset);
					}
				}
			}
			else
			{
				throw new NotSupportedException("Multi body not supported yet. Total body size is " + bodySize + " and first body is " + length + " bytes");
			}
		}

		private async Task<BasicProperties> ReadRestOfContentHeader()
		{
			var presence = _reader.ReadUInt16();

			BasicProperties properties;

			if (presence == 0) // no header content
			{
				properties = BasicProperties.Empty;
			}
			else
			{
				properties = new BasicProperties {_presenceSWord = presence};
				if (properties.IsContentTypePresent) { properties.ContentType = await _amqpReader.ReadShortStr(); }
				if (properties.IsContentEncodingPresent) { properties.ContentEncoding = await _amqpReader.ReadShortStr(); }
				if (properties.IsHeadersPresent) { properties.Headers = await _amqpReader.ReadTable(); }
				if (properties.IsDeliveryModePresent) { properties.DeliveryMode = await _amqpReader.ReadOctet(); }
				if (properties.IsPriorityPresent) { properties.Priority = await _amqpReader.ReadOctet(); }
				if (properties.IsCorrelationIdPresent) { properties.CorrelationId = await _amqpReader.ReadShortStr(); }
				if (properties.IsReplyToPresent) { properties.ReplyTo = await _amqpReader.ReadShortStr(); }
				if (properties.IsExpirationPresent) { properties.Expiration = await _amqpReader.ReadShortStr(); }
				if (properties.IsMessageIdPresent) { properties.MessageId = await _amqpReader.ReadShortStr(); }
				if (properties.IsTimestampPresent) { properties.Timestamp = await _amqpReader.ReadTimestamp(); }
				if (properties.IsTypePresent) { properties.Type = await _amqpReader.ReadShortStr(); }
				if (properties.IsUserIdPresent) { properties.UserId = await _amqpReader.ReadShortStr(); }
				if (properties.IsAppIdPresent) { properties.AppId = await _amqpReader.ReadShortStr(); }
				if (properties.IsClusterIdPresent) { properties.ClusterId = await _amqpReader.ReadShortStr(); }
			}

			int frameEndMarker = await _reader.ReadByte();
			if (frameEndMarker != AmqpConstants.FrameEnd) throw new Exception("Expecting frameend!");

			return properties;
		}

		public async Task Read_BasicConsumeOk(Func<string, Task> continuation)
		{
			var consumerTag = await _amqpReader.ReadShortStr();

			Console.WriteLine("< BasicConsumeOk ");

			await continuation(consumerTag);
		}

		public async Task Read_BasicReturn(Func<ushort, string, string, string, uint, BasicProperties, Stream, Task> continuation)
		{
			var replyCode = _amqpReader.ReadShort();
			var replyText = await _amqpReader.ReadShortStr();
			var exchange = await _amqpReader.ReadShortStr();
			var routingKey = await _amqpReader.ReadShortStr();

			var frameEndMarker = await _amqpReader.ReadOctet();
			if (frameEndMarker != AmqpConstants.FrameEnd) throw new Exception("Expecting frameend!");

			// Frame Header / Content header

			var frameHeaderStart = await _amqpReader.ReadOctet();
			if (frameHeaderStart != AmqpConstants.FrameHeader) throw new Exception("Expecting Frame Header");

			await _reader.SkipBy(4 + 2 + 2 + 2);
			// ushort channel = _reader.ReadUInt16();
			// int payloadLength = await _reader.ReadInt32();
			// var classId = _reader.ReadUInt16();
			// var weight = _reader.ReadUInt16();
			var bodySize = (long)await _reader.ReadUInt64();

			var properties = await ReadRestOfContentHeader();

			// Frame Body(s)

			frameHeaderStart = await _reader.ReadByte();
			if (frameHeaderStart != AmqpConstants.FrameBody) throw new Exception("Expecting Frame Body");

			await _reader.SkipBy(2); // channel = _reader.ReadUInt16();
			uint length = _reader.ReadUInt32();

			// must leave pending Frame end

			if (length == bodySize)
			{
				// continuation(replyCode, replyText, exchange, routingKey);
				
				var marker = new RingBufferPositionMarker(_reader._ringBufferStream._ringBuffer);

				await
					continuation(replyCode, replyText, exchange, 
						routingKey, length, properties, (Stream) _reader._ringBufferStream);

				if (marker.LengthRead < length)
				{
					checked
					{
						int offset = (int)(length - marker.LengthRead);
						await _reader.SkipBy(offset);
					}
				}
			}
			else
			{
				throw new NotSupportedException("Multi body not supported yet. Total body size is " + bodySize + " and first body is " + length + " bytes");
			}
		}
	}
}