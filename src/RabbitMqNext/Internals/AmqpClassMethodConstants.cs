namespace RabbitMqNext.Internals
{
	public static class AmqpClassMethodConstants
	{
		public const int ConnectionStart = 655370;

		public const int ConnectionStartOk = 655371;

		public const int ConnectionTune = 655390;

		public const int ConnectionClose = 655410;

		public const int ConnectionOpenOk = 655401;
	}

	public static class AmqpClassMethodChannelLevelConstants
	{
		public const int ChannelOpenOk = (20 << 16) | 11;

		public const int ChannelFlowOk = (20 << 16) | 21;

		public const int ChannelCloseOk = (20 << 16) | 41;

		public const int ExchangeDeclareOk = (40 << 16) | 11;

		public const int ExchangeBindOk = (40 << 16) | 31;

		public const int ExchangeUnbindOk = (40 << 16) | 51;

		public const int QueueDeclareOk = (50 << 16) | 11;

		public const int QueueBindOk = (50 << 16) | 21;

		public const int QueueUnbindOk = (50 << 16) | 51;

		public const int BasicQosOk = (60 << 16) | 11;

		public const int BasicConsumeOk = (60 << 16) | 21;

//		public const int BasicPublish = (60 << 16) | 40;

		// public const int BasicReturn = (60 << 16) | 50;

		// public const int BasicDeliver = (60 << 16) | 60;

		// public const int BasicAck = (60 << 16) | 80;
	}
}