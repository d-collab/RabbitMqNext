namespace RabbitMqNext.Internals
{
	using System;
	using System.Collections.Generic;
	using System.Text;
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

				writer.WriteFrameStart(AmqpConstants.FrameMethod, channel, payloadSize, classId, methodId);

//				writer.WriteUShort(classId);
//				writer.WriteUShort(methodId);
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

				writer.WriteFrameStart(AmqpConstants.FrameMethod, channel, payloadSize, classId, methodId);

//				writer.WriteUShort(classId);
//				writer.WriteUShort(methodId);

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
				writer.WriteFrameStart(AmqpConstants.FrameHeader, channel, payloadSize, 60, 0);
//				writer.WriteUShort((ushort)60);
//				writer.WriteUShort((ushort)0); // weight. not used
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

			// return (writer, channel, classId, methodId, args) =>
			{
				uint payloadSize = (uint)(8 + 5);

				writer.WriteFrameStart(AmqpConstants.FrameMethod, channel, payloadSize, classId, methodId);

//				writer.WriteUShort(classId);
//				writer.WriteUShort(methodId);

				writer.WriteULong(b_args.deliveryTag);
				writer.WriteBit(b_args.multiple);

				writer.WriteOctet(AmqpConstants.FrameEnd);
			};
		}

		internal static void InternalBasicNAck(AmqpPrimitivesWriter writer, ushort channel, ushort classId, ushort methodId, object args)
		{
			var b_args = args as FrameParameters.BasicNAckArgs;

			uint payloadSize = (uint)(8 + 5);

			writer.WriteFrameStart(AmqpConstants.FrameMethod, channel, payloadSize, classId, methodId);
//			writer.WriteUShort(classId);
//			writer.WriteUShort(methodId);
			writer.WriteULong(b_args.deliveryTag);
			writer.WriteBits(b_args.multiple, b_args.requeue);

			writer.WriteOctet(AmqpConstants.FrameEnd);
		}

		internal static void InternalBasicPublish(AmqpPrimitivesWriter writer, ushort channel, ushort classId, ushort methodId, object args)
		{
			var basicPub = args as FrameParameters.BasicPublishArgs;

//			try
			{
				var buffer = basicPub.buffer;
				var properties = basicPub.properties;

				// First frame: Method
				uint payloadSize = (uint) (9 + basicPub.exchange.Length + basicPub.routingKey.Length);
				writer.WriteFrameStart(AmqpConstants.FrameMethod, channel, payloadSize, classId, methodId);
//				writer.WriteFrameWithPayloadFirst(AmqpConstants.FrameMethod, channel, w =>
				var w = writer;
				{
//					w.WriteUShort(classId);
//					w.WriteUShort(methodId);
					w.WriteUShort(0); // reserved1
					w.WriteShortstr(basicPub.exchange);
					w.WriteShortstr(basicPub.routingKey);
					w.WriteBits(basicPub.mandatory, basicPub.immediate);
				} //);
				writer.WriteOctet(AmqpConstants.FrameEnd);

				WriteBasicPropertiesAsHeader(writer, channel, (ulong)buffer.Count, properties);

				// what's the max frame size we can write?
				if (!writer.FrameMaxSize.HasValue) throw new Exception("wtf? no frame max set!");
				
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