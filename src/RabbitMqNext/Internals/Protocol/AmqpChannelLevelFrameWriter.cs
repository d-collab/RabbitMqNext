namespace RabbitMqNext.Internals
{
	using System;
	using System.Collections.Generic;
	using WriterDelegate = System.Action<AmqpPrimitivesWriter, ushort, ushort, ushort, object>;

	static class AmqpChannelLevelFrameWriter
	{
		private const int EmptyFrameSize = 8;

		public static WriterDelegate ChannelOpen()
		{
			const uint payloadSize = 4 + 1;

			return (writer, channel, classId, methodId, args) =>
			{
				Console.WriteLine("ChannelOpen");

				writer.WriteFrameStart(AmqpConstants.FrameMethod, channel, payloadSize);

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
			return (writer, channel, classId, methodId, args) =>
			{
				Console.WriteLine("ExchangeDeclare");

				writer.WriteFrameWithPayloadFirst(AmqpConstants.FrameMethod, channel, w =>
				{
					w.WriteUShort(classId);
					w.WriteUShort(methodId);

					w.WriteUShort(0); // reserved
					w.WriteShortstr(exchange);
					w.WriteShortstr(type);
					w.WriteBits(passive, durable, autoDelete, @internal, !waitConfirmation);
					w.WriteTable(arguments);
				});
			};
		}

		public static WriterDelegate QueueDeclare(string queue, bool passive, bool durable, bool exclusive,
										  bool autoDelete, IDictionary<string, object> arguments, bool waitConfirmation)
		{
			return (writer, channel, classId, methodId, args) =>
			{
				Console.WriteLine("QueueDeclare");

				writer.WriteFrameWithPayloadFirst(AmqpConstants.FrameMethod, channel, w =>
				{
					w.WriteUShort(classId);
					w.WriteUShort(methodId);

					w.WriteUShort(0);
					w.WriteShortstr(queue);
					w.WriteBits(passive, durable, exclusive, autoDelete, !waitConfirmation);
					w.WriteTable(arguments);
				});
			};
		}

		public static WriterDelegate BasicPublish()
		{
			return InternalBasicPublish;
		}

		public static void ChannelClose(AmqpPrimitivesWriter writer, ushort channel, ushort classId, ushort methodId, object args)
		{
			var closeArgs = (FrameParameters.CloseParams) args;

			writer.WriteFrameWithPayloadFirst(AmqpConstants.FrameMethod, channel, (w) =>
			{
				w.WriteUShort(classId);
				w.WriteUShort(methodId);

				w.WriteUShort(closeArgs.replyCode);
				w.WriteShortstr(closeArgs.replyText);
				w.WriteUShort(classId);
				w.WriteUShort(methodId);
			});
		}

		public static void ChannelCloseOk(AmqpPrimitivesWriter writer, ushort channel, ushort classId, ushort methodId, object args)
		{
			AmqpConnectionFrameWriter.WriteEmptyMethodFrame(writer, channel, classId, methodId);
		}

		public static WriterDelegate BasicConsume(string queue, string consumerTag, 
			bool withoutAcks, bool exclusive, IDictionary<string, object> arguments, bool waitConfirmation)
		{
			return (writer, channel, classId, methodId, args) =>
			{
				Console.WriteLine("BasicConsume");

				writer.WriteFrameWithPayloadFirst(AmqpConstants.FrameMethod, channel, w =>
				{
					w.WriteUShort(classId);
					w.WriteUShort(methodId);

					w.WriteUShort(0);
					w.WriteShortstr(queue);
					w.WriteShortstr(consumerTag);
					w.WriteBits(false, withoutAcks, exclusive, !waitConfirmation);
					w.WriteTable(arguments);
				});
			};
		}

		public static WriterDelegate BasicQos(uint prefetchSize, ushort prefetchCount, bool global)
		{
			const int payloadSize = 11;

			return (writer, channel, classId, methodId, args) =>
			{
				Console.WriteLine("ChannelOpen");

				writer.WriteFrameStart(AmqpConstants.FrameMethod, channel, payloadSize);

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
			return (writer, channel, classId, methodId, args) =>
			{
				Console.WriteLine("QueueBind");

				writer.WriteFrameWithPayloadFirst(AmqpConstants.FrameMethod, channel, w =>
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
			};
		}

		private static void WriteBasicPropertiesAsHeader(AmqpPrimitivesWriter writer, 
			ushort channel, ulong bodySize, BasicProperties properties)
		{
			if (properties.IsEmpty)
			{
				uint payloadSize = 4 + 8 + 2;
				writer.WriteFrameStart(AmqpConstants.FrameHeader, channel, payloadSize);

				writer.WriteUShort((ushort)60);
				writer.WriteUShort((ushort)0); // weight. not used
				writer.WriteULong(bodySize);

				// no support for continuation. must be less than 15 bits used
				writer.WriteUShort(properties._presenceSWord);

				writer.WriteOctet(AmqpConstants.FrameEnd);
			}
			else
			{
				writer.WriteFrameWithPayloadFirst(AmqpConstants.FrameHeader, channel, w =>
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
			}
		}

		internal static void InternalBasicPublish(AmqpPrimitivesWriter writer, ushort channel, ushort classId, ushort methodId, object args)
		{
			var basicPub = args as FrameParameters.BasicPublishArgs;

//			try
			{
				var buffer = basicPub.buffer;
				var properties = basicPub.properties;
			
				writer.WriteFrameWithPayloadFirst(AmqpConstants.FrameMethod, channel, w =>
				{
					w.WriteUShort(classId);
					w.WriteUShort(methodId);

					w.WriteUShort(0); // reserved1
					w.WriteShortstr(basicPub.exchange);
					w.WriteShortstr(basicPub.routingKey);
					w.WriteBits(basicPub.mandatory, basicPub.immediate);
				});

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
			}
//			finally
			{
				basicPub.Done();	
			}
		}
	}
}