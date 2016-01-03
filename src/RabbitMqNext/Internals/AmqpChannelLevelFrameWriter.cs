namespace RabbitMqNext.Internals
{
	using System;
	using System.Collections.Generic;
	using System.Text;
	using WriterDelegate = System.Action<AmqpPrimitivesWriter, ushort, ushort, ushort>;

	static class AmqpChannelLevelFrameWriter
	{
		private const int EmptyFrameSize = 8;

		public static WriterDelegate ChannelOpen()
		{
			const uint payloadSize = 4 + 1;

			return (writer, channel, classId, methodId) =>
			{
				Console.WriteLine("ChannelOpen");

				writer.WriteOctet(AmqpConstants.FrameMethod);
				writer.WriteUShort(channel); // channel
				writer.WriteLong(payloadSize); // payload size

				writer.WriteUShort(classId);
				writer.WriteUShort(methodId);
				writer.WriteShortstr("");

				writer.WriteOctet(AmqpConstants.FrameEnd);
			};
		}

		public static WriterDelegate ExchangeDeclare(string exchange, string type, 
			bool durable, bool autoDelete, 
			IDictionary<string, object> arguments, bool @internal, bool passive, bool waitConfirmation)
		{
			return (writer, channel, classId, methodId) =>
			{
				Console.WriteLine("ExchangeDeclare");

				writer.WriteOctet(AmqpConstants.FrameMethod);
				writer.WriteUShort(channel); // channel
				
				writer.WriteWithPayloadFirst(w =>
				{
					w.WriteUShort(classId);
					w.WriteUShort(methodId);

					w.WriteUShort(0); // reserved
					w.WriteShortstr(exchange);
					w.WriteShortstr(type);
					w.WriteBits(passive, durable, autoDelete, @internal, !waitConfirmation);
					w.WriteTable(arguments);
				});
				
				writer.WriteOctet(AmqpConstants.FrameEnd);
			};
		}

		public static WriterDelegate QueueDeclare(string queue, bool passive, bool durable, bool exclusive,
										  bool autoDelete, IDictionary<string, object> arguments, bool waitConfirmation)
		{
			return (writer, channel, classId, methodId) =>
			{
				Console.WriteLine("QueueDeclare");

				writer.WriteOctet(AmqpConstants.FrameMethod);
				writer.WriteUShort(channel);

				writer.WriteWithPayloadFirst(w =>
				{
					w.WriteUShort(classId);
					w.WriteUShort(methodId);

					w.WriteUShort(0);
					w.WriteShortstr(queue);
					w.WriteBits(passive, durable, exclusive, autoDelete, !waitConfirmation);
					w.WriteTable(arguments);
				});

				writer.WriteOctet(AmqpConstants.FrameEnd);
			};
		}

		public static WriterDelegate BasicPublish(string exchange, string routingKey, bool mandatory, bool immediate,
								 BasicProperties properties, ArraySegment<byte> buffer)
		{
			return (writer, channel, classId, methodId) =>
			{
				writer.WriteOctet(AmqpConstants.FrameMethod);
				writer.WriteUShort(channel);

				var payloadSize = (uint)(9 + Encoding.UTF8.GetByteCount(exchange) + Encoding.UTF8.GetByteCount(routingKey));
				writer.WriteLong(payloadSize);

				writer.WriteUShort(classId);
				writer.WriteUShort(methodId);

				writer.WriteUShort(0); // reserved1
				writer.WriteShortstr(exchange);
				writer.WriteShortstr(routingKey);
				writer.WriteBits(mandatory, immediate);

				writer.WriteOctet(AmqpConstants.FrameEnd);

				WriteBasicPropertiesAsHeader(writer, channel, (ulong)buffer.Count, properties);

				// what's the max frame size we can write?
				if (!writer.FrameMaxSize.HasValue) 
					throw new Exception("wtf? no frame max set!");
				
				var maxSubFrameSize =
					writer.FrameMaxSize == 0 ? (int)buffer.Count :
											   (int)writer.FrameMaxSize.Value - EmptyFrameSize;

				// write frames limited by the max size
				int written = 0;
				while (written < buffer.Count)
				{
					writer.WriteOctet(AmqpConstants.FrameBody);
					writer.WriteUShort(channel);

					var countToWrite = Math.Min(buffer.Count - written, maxSubFrameSize);
					writer.WriteLong((uint)countToWrite); // payload size

					writer.WriteRaw(buffer.Array, buffer.Offset + written, countToWrite);
					written += countToWrite;

					writer.WriteOctet(AmqpConstants.FrameEnd);
				}
			};
		}

		public static WriterDelegate BasicQos(uint prefetchSize, ushort prefetchCount, bool global)
		{
			const int payloadSize = 11;

			return (writer, channel, classId, methodId) =>
			{
				Console.WriteLine("ChannelOpen");

				writer.WriteOctet(AmqpConstants.FrameMethod);
				writer.WriteUShort(channel); 
				writer.WriteLong(payloadSize);

				writer.WriteUShort(classId);
				writer.WriteUShort(methodId);

				writer.WriteLong(prefetchSize);
				writer.WriteUShort(prefetchCount);
				writer.WriteBit(global);

				writer.WriteOctet(AmqpConstants.FrameEnd);
			};
		}

		public static WriterDelegate QueueBind(string queue, string exchange, 
			string routingKey, IDictionary<string, object> arguments, 
			bool waitConfirmation)
		{
			return (writer, channel, classId, methodId) =>
			{
				Console.WriteLine("QueueBind");

				writer.WriteOctet(AmqpConstants.FrameMethod);
				writer.WriteUShort(channel);

				writer.WriteWithPayloadFirst(w =>
				{
					w.WriteUShort(classId);
					w.WriteUShort(methodId);

					w.WriteUShort(0);
					w.WriteShortstr(queue);
					w.WriteShortstr(exchange);
					w.WriteShortstr(routingKey);
					w.WriteBits(!waitConfirmation);
					w.WriteTable(arguments);
				});

				writer.WriteOctet(AmqpConstants.FrameEnd);
			};
		}

		private static void WriteBasicPropertiesAsHeader(AmqpPrimitivesWriter writer, ushort channel, ulong bodySize, BasicProperties properties)
		{
			writer.WriteOctet(AmqpConstants.FrameHeader);
			writer.WriteUShort(channel);

			writer.WriteWithPayloadFirst(w =>
			{
				w.WriteUShort((ushort)60);
				w.WriteUShort((ushort)0); // weight. not used
				w.WriteULong(bodySize);

				// no support for continuation. must be less than 15 bits used
				w.WriteUShort(properties._presenceSWord);

				if (properties.IsContentTypePresent) { w.WriteShortstr(properties.ContentType); }
				if (properties.IsContentEncodingPresent) { w.WriteShortstr(properties.ContentEncoding); }
				if (properties.IsHeadersPresent) { w.WriteTable(properties.Headers); }
				if (properties.IsDeliveryModePresent) { w.WriteOctet(properties.DeliveryMode); }
				if (properties.IsPriorityPresent) { w.WriteOctet(properties.Priority); }
				if (properties.IsCorrelationIdPresent) { w.WriteShortstr(properties.CorrelationId); }
				if (properties.IsReplyToPresent) { w.WriteShortstr(properties.ReplyTo); }
				if (properties.IsExpirationPresent) { w.WriteShortstr(properties.Expiration); }
				if (properties.IsMessageIdPresent) { w.WriteShortstr(properties.MessageId); }
				if (properties.IsTimestampPresent) { w.WriteTimestamp(properties.Timestamp); }
				if (properties.IsTypePresent) { w.WriteShortstr(properties.Type); }
				if (properties.IsUserIdPresent) { w.WriteShortstr(properties.UserId); }
				if (properties.IsAppIdPresent) { w.WriteShortstr(properties.AppId); }
				if (properties.IsClusterIdPresent) { w.WriteShortstr(properties.ClusterId); }
			});

			writer.WriteOctet(AmqpConstants.FrameEnd);
		}
	}
}