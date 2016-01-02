namespace RabbitMqNext.Internals
{
	using System;
	using System.Collections.Generic;
	using System.Text;


	static class AmqpConnectionFrameWriter
	{
		private static readonly byte[] GreetingPayload;

		static AmqpConnectionFrameWriter()
		{
			var v = Encoding.ASCII.GetBytes("AMQP");
			GreetingPayload = new byte[8];
			Buffer.BlockCopy(v, 0, GreetingPayload, 0, v.Length);
			GreetingPayload[4] = 0;
			GreetingPayload[5] = 0; // major
			GreetingPayload[6] = 9; // minor 
			GreetingPayload[7] = 1; // revision
		}

		public static Action<AmqpPrimitivesWriter> Greeting()
		{
			return writer =>
			{
				writer.WriteRaw(GreetingPayload, 0, GreetingPayload.Length);
			};
		}

		public static Action<AmqpPrimitivesWriter> ConnectionTuneOk(ushort channelMax, uint frameMax, ushort heartbeat)
		{
			const int payloadSize = 12; // 4 shorts + 1 int

			return writer =>
			{
				Console.WriteLine("ConnectionTuneOk");

				writer.WriteOctet(AmqpConstants.FrameMethod);
				writer.WriteUShort((ushort)0); // channel
				writer.WriteLong(payloadSize); // payload size

				writer.WriteUShort((ushort)10);
				writer.WriteUShort((ushort)31);
				writer.WriteUShort(channelMax);
				writer.WriteLong(frameMax);
				writer.WriteUShort(heartbeat);

				writer.WriteOctet(AmqpConstants.FrameEnd);
			};
		}

		public static Action<AmqpPrimitivesWriter> ConnectionStartOk(
			IDictionary<string, object> clientProperties,
			string mechanism, byte[] response, string locale)
		{
			return writer =>
			{
				Console.WriteLine("ConnectionStartOk");

				writer.WriteOctet(AmqpConstants.FrameMethod);
				writer.WriteUShort((ushort)0); // channel

				writer.WriteWithPayloadFirst((w) =>
				{
					w.WriteUShort((ushort)10);
					w.WriteUShort((ushort)11);

					w.WriteTable(clientProperties);
					// w.WriteTable(null);
					w.WriteShortstr(mechanism);
					w.WriteLongbyte(response);
					w.WriteShortstr(locale);
				});

				writer.WriteOctet(AmqpConstants.FrameEnd);
			};
		}

		public static Action<AmqpPrimitivesWriter> ConnectionOpen(string vhost, string caps, bool insist)
		{
			return writer =>
			{
				Console.WriteLine("ConnectionOpen");

				writer.WriteOctet(AmqpConstants.FrameMethod);
				writer.WriteUShort((ushort)0); // channel

				writer.WriteWithPayloadFirst((w) =>
				{
					w.WriteUShort((ushort)10);
					w.WriteUShort((ushort)40);

					w.WriteShortstr(vhost);
					w.WriteShortstr(caps);
					w.WriteBit(insist);
				});

				writer.WriteOctet(AmqpConstants.FrameEnd);
			};
		}

		public static Action<AmqpPrimitivesWriter> ConnectionCloseOk()
		{
			return writer =>
			{

			};
		}
	}
}
