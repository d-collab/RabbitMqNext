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

			string queue = _amqpReader.ReadShortStr();
			uint messageCount = _amqpReader.ReadLong();
			uint consumerCount = _amqpReader.ReadLong();

			await continuation(queue, messageCount, consumerCount);
		}

		public async Task Read_Channel_Close2(Func<ushort, string, ushort, ushort, Task> continuation)
		{
			ushort replyCode = _amqpReader.ReadShort();
			string replyText = _amqpReader.ReadShortStr();
			ushort classId = _amqpReader.ReadShort();
			ushort methodId = _amqpReader.ReadShort();

			Console.WriteLine("< channel close coz  " + replyText + " in class  " + classId + " methodif " + methodId);

			await continuation(replyCode, replyText, classId, methodId);
		}

		public async Task Read_BasicDelivery(Func<string, ulong, bool, string, string, int, BasicProperties, Stream, Task> continuation)
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

			BasicProperties properties = ReadRestOfContentHeader();

			// Frame Body(s)

			// Support just single body at this moment.

			frameHeaderStart = _reader.ReadByte();
			if (frameHeaderStart != AmqpConstants.FrameBody) throw new Exception("Expecting Frame Body");

			// await _reader.SkipBy(2);
			channel =   _reader.ReadUInt16();
			uint length = _reader.ReadUInt32();

			// Pending Frame end

			if (length == bodySize)
			{
				var marker = new RingBufferPositionMarker(_reader._ringBufferStream._ringBuffer);

				await continuation(consumerTag, deliveryTag, redelivered, exchange,
					routingKey, (int)length, properties, (Stream) _reader._ringBufferStream);

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

		private BasicProperties ReadRestOfContentHeader()
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
				if (properties.IsContentTypePresent) { properties.ContentType =  _amqpReader.ReadShortStr(); }
				if (properties.IsContentEncodingPresent) { properties.ContentEncoding =  _amqpReader.ReadShortStr(); }
				if (properties.IsHeadersPresent) { properties.Headers =  _amqpReader.ReadTable(); }
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

			byte frameEndMarker = _reader.ReadByte();
			if (frameEndMarker != AmqpConstants.FrameEnd) throw new Exception("Expecting frameend!");

			return properties;
		}

		public void Read_BasicConsumeOk(Action<string> continuation)
		{
			var consumerTag = _amqpReader.ReadShortStr();

			Console.WriteLine("< BasicConsumeOk " + DateTime.Now.TimeOfDay.TotalSeconds);

			continuation(consumerTag);
		}

		public async Task Read_BasicReturn(Func<ushort, string, string, string, int, BasicProperties, Stream, Task> continuation)
		{
			ushort replyCode = _amqpReader.ReadShort();
			string replyText = _amqpReader.ReadShortStr();
			string exchange = _amqpReader.ReadShortStr();
			string routingKey = _amqpReader.ReadShortStr();

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
			var bodySize = (long) _reader.ReadUInt64();

			BasicProperties properties = ReadRestOfContentHeader();

			// Frame Body(s)

			frameHeaderStart = _reader.ReadByte();
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
						routingKey, (int)length, properties, (Stream) _reader._ringBufferStream);

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

		public void Read_BasicAck(Action<ulong, bool> continuation)
		{
			ulong deliveryTags = _amqpReader.ReadULong();
			bool multiple = _amqpReader.ReadBits() != 0;

			Console.WriteLine("< BasicAck from server for  " + deliveryTags + " multiple:  " + multiple);

			continuation(deliveryTags, multiple);
		}

		public void Read_BasicNAck(Action<ulong, bool, bool> continuation)
		{
			ulong deliveryTags = _amqpReader.ReadULong();
			byte bits = _amqpReader.ReadBits();
			bool multiple = (bits & 1) != 0;
			bool requeue = (bits & 2) != 0;

			Console.WriteLine("< BasicNAck from server for  " + deliveryTags + " multiple:  " + multiple + " requeue " + requeue);

			continuation(deliveryTags, multiple, requeue);
		}

		public void Read_ChannelFlow(Action<bool> continuation)
		{
			bool isActive = _amqpReader.ReadBits() != 0;

			Console.WriteLine("< ChannelFlow from server for  " + isActive);

			continuation(isActive);
		}
	}
}