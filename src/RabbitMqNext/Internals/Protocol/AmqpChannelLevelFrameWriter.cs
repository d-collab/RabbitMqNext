namespace RabbitMqNext.Internals
{
	using System;
	using System.Collections.Generic;
	using System.Text;
	using WriterDelegate = System.Action<AmqpPrimitivesWriter, ushort, ushort, ushort, object>;

	static class AmqpChannelLevelFrameWriter
	{
		private const string LogSource = "AmqpChannelLevelFrameWriter";

		private const int EmptyFrameSize = 8;

		public static WriterDelegate ChannelOpen()
		{
			const uint payloadSize = 4 + 1;

			return (writer, channel, classId, methodId, args) =>
			{
				if (LogAdapter.ProtocolLevelLogEnabled)
					LogAdapter.LogDebug("AmqpChannelLevelFrameWriter", "> ChannelOpen");

				writer.WriteFrameStart(AmqpConstants.FrameMethod, channel, payloadSize, classId, methodId);

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
				if (LogAdapter.ProtocolLevelLogEnabled)
					LogAdapter.LogDebug(LogSource, "> ExchangeDeclare " + exchange);

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

		public static WriterDelegate ExchangeDelete(string exchange, IDictionary<string, object> arguments, bool waitConfirmation)
		{
			return (writer, channel, classId, methodId, args) =>
			{
				if (LogAdapter.ProtocolLevelLogEnabled)
					LogAdapter.LogDebug(LogSource, "> ExchangeDelete " + exchange);

				writer.WriteFrameWithPayloadFirst(AmqpConstants.FrameMethod, channel, w =>
				{
					w.WriteUShort(classId);
					w.WriteUShort(methodId);

					w.WriteUShort(0); // reserved
					w.WriteShortstr(exchange);
					w.WriteBits(false, !waitConfirmation);
					w.WriteTable(arguments);
				});
			};
		}

		public static WriterDelegate QueueDeclare(string queue, bool passive, bool durable, bool exclusive,
			bool autoDelete, IDictionary<string, object> arguments, bool waitConfirmation)
		{
			return (writer, channel, classId, methodId, args) =>
			{
				if (LogAdapter.ProtocolLevelLogEnabled)
					LogAdapter.LogDebug(LogSource, "> QueueDeclare " + queue);


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

		public static void ChannelClose(AmqpPrimitivesWriter writer, ushort channel, ushort classId, ushort methodId, object args)
		{
			var closeArgs = (FrameParameters.CloseParams) args;

			writer.WriteFrameWithPayloadFirst(AmqpConstants.FrameMethod, channel, (w) =>
			{
				if (LogAdapter.ProtocolLevelLogEnabled)
					LogAdapter.LogDebug(LogSource, "> ChannelClose " + channel);

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
			if (LogAdapter.ProtocolLevelLogEnabled)
				LogAdapter.LogDebug(LogSource, "> ChannelCloseOk " + channel);

			AmqpConnectionFrameWriter.WriteEmptyMethodFrame(writer, channel, classId, methodId);
		}

		public static WriterDelegate BasicConsume(string queue, string consumerTag, 
			bool withoutAcks, bool exclusive, IDictionary<string, object> arguments, bool waitConfirmation)
		{
			return (writer, channel, classId, methodId, args) =>
			{
				if (LogAdapter.ProtocolLevelLogEnabled)
					LogAdapter.LogDebug(LogSource, "> BasicConsume " + queue);

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
				if (LogAdapter.ProtocolLevelLogEnabled)
					LogAdapter.LogDebug(LogSource, "> BasicQos : prefetch " + prefetchCount);

				writer.WriteFrameStart(AmqpConstants.FrameMethod, channel, payloadSize, classId, methodId);

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
				if (LogAdapter.ProtocolLevelLogEnabled)
					LogAdapter.LogDebug(LogSource, "> QueueBind : " + queue + "|" + exchange + "|" + routingKey);

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
			properties.Prepare(); // Ensure IsHeadersSet bit is correct

			if (properties.IsEmpty)
			{
				// short cut when it's empty

				const uint payloadSize = 4 + 8 + 2;
				writer.WriteFrameStart(AmqpConstants.FrameHeader, channel, payloadSize, 60, 0);
				writer.WriteULong(bodySize);
				// no support for continuation. must be less than 15 bits used
				writer.WriteUShort(properties._presenceSWord);
				writer.WriteOctet(AmqpConstants.FrameEnd);
			}
			else
			{
				writer.WriteFrameHeader(channel, bodySize, properties);
			}
		}

		internal static void InternalBasicAck(AmqpPrimitivesWriter writer, ushort channel, ushort classId, ushort methodId, object args)
		{
			var b_args = args as FrameParameters.BasicAckArgs;

			if (LogAdapter.ProtocolLevelLogEnabled)
				LogAdapter.LogDebug(LogSource, "> BasicAck : " + b_args.deliveryTag);

			{
				uint payloadSize = (uint)(8 + 5);

				writer.WriteFrameStart(AmqpConstants.FrameMethod, channel, payloadSize, classId, methodId);

				writer.WriteULong(b_args.deliveryTag);
				writer.WriteBit(b_args.multiple);

				writer.WriteOctet(AmqpConstants.FrameEnd);
			};
		}

		internal static void InternalBasicNAck(AmqpPrimitivesWriter writer, ushort channel, ushort classId, ushort methodId, object args)
		{
			var b_args = args as FrameParameters.BasicNAckArgs;

			if (LogAdapter.ProtocolLevelLogEnabled)
				LogAdapter.LogDebug(LogSource, "> BasicNAck : " + b_args.deliveryTag);

			uint payloadSize = (uint)(8 + 5);

			writer.WriteFrameStart(AmqpConstants.FrameMethod, channel, payloadSize, classId, methodId);
			writer.WriteULong(b_args.deliveryTag);
			writer.WriteBits(b_args.multiple, b_args.requeue);

			writer.WriteOctet(AmqpConstants.FrameEnd);
		}

		internal static void InternalBufferedBasicPublish(AmqpPrimitivesWriter writerX, ushort channel, ushort classId, ushort methodId,
			object args)
		{
			var memStream = writerX._memStreamPool.GetObject();

			try
			{
				InternalBasicPublish(memStream._writer2, channel, classId, methodId, args);

				writerX._writer.Write(memStream._memoryStream.InternalBuffer, 0, (int)memStream._memoryStream.Position);
			}
			finally
			{
				writerX._memStreamPool.PutObject(memStream);
			}
		}

		internal static void InternalBasicPublish(AmqpPrimitivesWriter writer, ushort channel, ushort classId, ushort methodId, object args)
		{
			var basicPub = args as FrameParameters.BasicPublishArgs;

//			try
			{
				var userBuffer = basicPub.buffer;
				var properties = basicPub.properties;

				// First frame: Method
				uint payloadSize = (uint) (9 + basicPub.exchange.Length + basicPub.routingKey.Length);
				writer.WriteFrameStart(AmqpConstants.FrameMethod, channel, payloadSize, classId, methodId);
				var w = writer;
				{
					w.WriteUShort(0); // reserved1
					w.WriteShortstr(basicPub.exchange);
					w.WriteShortstr(basicPub.routingKey);
					w.WriteBits(basicPub.mandatory, false /* immediate*/ );
				}
				writer.WriteOctet(AmqpConstants.FrameEnd);
				// First frame completed

				// Header frame (basic properties)
				WriteBasicPropertiesAsHeader(writer, channel, (ulong)userBuffer.Count, properties);
				// Header frame completed

				// what's the max frame size we can write?
				if (!writer.FrameMaxSize.HasValue) throw new Exception("wtf? no frame max set!");
				

				// Frame body

				int maxSubFrameSize = (int)writer.FrameMaxSize.Value - EmptyFrameSize; // writer.FrameMaxSize == 0 ? buffer.Count : (int)writer.FrameMaxSize.Value - EmptyFrameSize;

				// write frames limited by the max size
				int written = 0;
				while (written < userBuffer.Count)
				{
					writer.WriteOctet(AmqpConstants.FrameBody);
					writer.WriteUShort(channel);

					var countToWrite = Math.Min(userBuffer.Count - written, maxSubFrameSize);
					writer.WriteLong((uint)countToWrite); // payload size

					writer.WriteRaw(userBuffer.Array, userBuffer.Offset + written, countToWrite);
					written += countToWrite;

					writer.WriteOctet(AmqpConstants.FrameEnd);
				}

				// Frame body completed
			}
//			finally
			{
				basicPub.Done();	
			}
		}

		public static WriterDelegate ConfirmSelect(bool noWait)
		{
			return (writer, channel, classId, methodId, args) =>
			{
				if (LogAdapter.ProtocolLevelLogEnabled)
					LogAdapter.LogDebug(LogSource, "> ConfirmSelect");

				ushort payloadSize = 1 + 4;
				writer.WriteFrameStart(AmqpConstants.FrameMethod, channel, payloadSize, classId, methodId);
				writer.WriteBit(noWait);
				writer.WriteOctet(AmqpConstants.FrameEnd);
			};
		}

		public static WriterDelegate BasicCancel(string consumerTag, bool waitConfirmation)
		{
			return (writer, channel, classId, methodId, args) =>
			{
				if (LogAdapter.ProtocolLevelLogEnabled)
					LogAdapter.LogDebug(LogSource, "> BasicCancel");

				writer.WriteFrameWithPayloadFirst(AmqpConstants.FrameMethod, channel, w =>
				{
					w.WriteUShort(classId);
					w.WriteUShort(methodId);

					w.WriteShortstr(consumerTag);
					w.WriteBits(!waitConfirmation);
				});
			};
		}

		public static WriterDelegate Recover(bool requeue)
		{
			return (writer, channel, classId, methodId, args) =>
			{
				if (LogAdapter.ProtocolLevelLogEnabled)
					LogAdapter.LogDebug(LogSource, "> Recover");

				ushort payloadSize = 1 + 4;
				writer.WriteFrameStart(AmqpConstants.FrameMethod, channel, payloadSize, classId, methodId);
				writer.WriteBit(requeue);
				writer.WriteOctet(AmqpConstants.FrameEnd);
			};
		}

		public static WriterDelegate ChannelFlowOk(bool isActive)
		{
			return (writer, channel, classId, methodId, args) =>
			{
				if (LogAdapter.ProtocolLevelLogEnabled)
					LogAdapter.LogDebug(LogSource, "> ChannelFlowOk " + isActive);

				ushort payloadSize = 1 + 4;
				writer.WriteFrameStart(AmqpConstants.FrameMethod, channel, payloadSize, classId, methodId);
				writer.WriteBit(isActive);
				writer.WriteOctet(AmqpConstants.FrameEnd);
			};
		}

		public static WriterDelegate ExchangeBind(string source, string destination, string routingKey, 
			IDictionary<string, object> arguments, bool waitConfirmation)
		{
			return (writer, channel, classId, methodId, args) =>
			{
				if (LogAdapter.ProtocolLevelLogEnabled)
					LogAdapter.LogDebug(LogSource, "> ExchangeBind " + source);

				writer.WriteFrameWithPayloadFirst(AmqpConstants.FrameMethod, channel, w =>
				{
					w.WriteUShort(classId);
					w.WriteUShort(methodId);

					w.WriteUShort(0); // reserved
					w.WriteShortstr(destination);
					w.WriteShortstr(source);
					w.WriteShortstr(routingKey);
					w.WriteBit(!waitConfirmation);
					w.WriteTable(arguments);
				});
			};
		}

		public static WriterDelegate ExchangeUnbind(string source, string destination, string routingKey, 
			IDictionary<string, object> arguments, bool waitConfirmation)
		{
			return (writer, channel, classId, methodId, args) =>
			{
				if (LogAdapter.ProtocolLevelLogEnabled)
					LogAdapter.LogDebug(LogSource, "> ExchangeUnbind " + source);

				writer.WriteFrameWithPayloadFirst(AmqpConstants.FrameMethod, channel, w =>
				{
					w.WriteUShort(classId);
					w.WriteUShort(methodId);

					w.WriteUShort(0); // reserved
					w.WriteShortstr(destination);
					w.WriteShortstr(source);
					w.WriteShortstr(routingKey);
					w.WriteBit(!waitConfirmation);
					w.WriteTable(arguments);
				});
			};
		}

		public static WriterDelegate QueuePurge(string queue, bool waitConfirmation)
		{
			return (writer, channel, classId, methodId, args) =>
			{
				if (LogAdapter.ProtocolLevelLogEnabled)
					LogAdapter.LogDebug(LogSource, "> QueuePurge " + queue);

				writer.WriteFrameWithPayloadFirst(AmqpConstants.FrameMethod, channel, w =>
				{
					w.WriteUShort(classId);
					w.WriteUShort(methodId);

					w.WriteUShort(0); // reserved
					w.WriteShortstr(queue);
					w.WriteBit(!waitConfirmation);
				});
			};
		}

		public static WriterDelegate QueueDelete(string queue, bool waitConfirmation)
		{
			return (writer, channel, classId, methodId, args) =>
			{
				if (LogAdapter.ProtocolLevelLogEnabled)
					LogAdapter.LogDebug(LogSource, "> QueueDelete " + queue);

				writer.WriteFrameWithPayloadFirst(AmqpConstants.FrameMethod, channel, w =>
				{
					w.WriteUShort(classId);
					w.WriteUShort(methodId);

					w.WriteUShort(0); // reserved
					w.WriteShortstr(queue);
					w.WriteBits(false /*ifUnused*/, false /*ifEmpty*/, !waitConfirmation);
				});
			};
		}

		public static WriterDelegate QueueUnbind(string queue, string exchange, string routingKey, 
			IDictionary<string, object> arguments)
		{
			return (writer, channel, classId, methodId, args) =>
			{
				if (LogAdapter.ProtocolLevelLogEnabled)
					LogAdapter.LogDebug(LogSource, "> QueueUnbind " + queue);

				writer.WriteFrameWithPayloadFirst(AmqpConstants.FrameMethod, channel, w =>
				{
					w.WriteUShort(classId);
					w.WriteUShort(methodId);

					w.WriteUShort(0); // reserved
					w.WriteShortstr(queue);
					w.WriteShortstr(exchange);
					w.WriteShortstr(routingKey);
					w.WriteTable(arguments);
				});
			};
		}
	}
}