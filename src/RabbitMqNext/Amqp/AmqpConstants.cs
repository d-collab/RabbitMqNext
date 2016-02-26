namespace RabbitMqNext.Internals
{
	internal static class AmqpConstants
	{
		public const int FrameMethod = 1;
		public const int FrameHeader = 2;
		public const int FrameBody = 3;
		public const int FrameHeartbeat = 8;
		public const int FrameMinSize = 4096;
		public const int FrameEnd = 206;
		public const int ReplySuccess = 200;
		public const int ContentTooLarge = 311;
		public const int NoConsumers = 313;
		public const int ConnectionForced = 320;
		public const int InvalidPath = 402;
		public const int AccessRefused = 403;
		public const int NotFound = 404;
		public const int ResourceLocked = 405;
		public const int PreconditionFailed = 406;
		public const int FrameError = 501;
		public const int SyntaxError = 502;
		public const int CommandInvalid = 503;
		public const int ChannelError = 504;
		public const int UnexpectedFrame = 505;
		public const int ResourceError = 506;
		public const int NotAllowed = 530;
		public const int NotImplemented = 540;
		public const int InternalError = 541;
	}

	public static class Amqp
	{
		public static class Channel
		{
			public const ushort ClassId = 20;

			public static class Methods
			{
				public const ushort ChannelOpen = 10;
				public const ushort ChannelOpenOk = 11;

				public const ushort ChannelClose = 40;
			}





			public static class Exchange
			{
				public const ushort ClassId = 40;

				public static class Methods
				{
					public const ushort ExchangeDeclare = 10;
					public const ushort ExchangeDeclareOk = 11;

					public const ushort ExchangeDelete = 20;
					public const ushort ExchangeDeleteOk = 21;

					public const ushort ExchangeBind = 30;
					public const ushort ExchangeBindOk = 31;

					public const ushort ExchangeUnBind = 40;
					public const ushort ExchangeUnBindOk = 51; // weird, I know!
				}
			}

			public static class Queue
			{
				public const ushort ClassId = 50;

				public static class Methods
				{
					public const ushort QueueDeclare = 10;
					public const ushort QueueDeclareOk = 11;

					public const ushort QueueBind = 20;
					public const ushort QueueBindOk = 21;

					public const ushort QueueUnbind = 50;
					public const ushort QueueUnbindOk = 51;

					public const ushort QueuePurge = 30;
					public const ushort QueuePurgeOk = 31;

					public const ushort QueueDelete = 40;
					public const ushort QueueDeleteOk = 41;
				}
			}
		}
	}
}