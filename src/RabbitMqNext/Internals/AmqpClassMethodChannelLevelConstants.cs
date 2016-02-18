namespace RabbitMqNext.Internals
{
	public static class AmqpClassMethodChannelLevelConstants
	{
		public const int ChannelOpenOk = (20 << 16) | 11;
		public const int ChannelFlow = (20 << 16) | 20;
		public const int ChannelFlowOk = (20 << 16) | 21;
		public const int ChannelClose = (20 << 16) | 40;
		public const int ChannelCloseOk = (20 << 16) | 41;

		public const int ExchangeDeclareOk = (40 << 16) | 11;
		public const int ExchangeBindOk = (40 << 16) | 31;
		public const int ExchangeUnbindOk = (40 << 16) | 51;
		public const int ExchangeDeleteOk = (40 << 16) | 21;

		public const int QueueDeclareOk = (50 << 16) | 11;
		public const int QueueBindOk = (50 << 16) | 21;
		public const int QueuePurgeOk = (50 << 16) | 31;
		public const int QueueDeleteOk = (50 << 16) | 41;
		public const int QueueUnbindOk = (50 << 16) | 51;

		public const int BasicQosOk = (60 << 16) | 11;
		public const int BasicConsumeOk = (60 << 16) | 21;
		public const int BasicReturn = (60 << 16) | 50;
		public const int BasicDeliver = (60 << 16) | 60;
		public const int BasicAck = (60 << 16) | 80;
		public const int BasicNAck = (60 << 16) | 120;
		public const int BasicReject = (60 << 16) | 90;
		public const int RecoverOk = (60 << 16) | 111;
		public const int CancelOk = (60 << 16) | 31;

		public const int ConfirmSelectOk = (85 << 16) | 11;
	}
}