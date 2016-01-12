namespace RabbitMqNext.Internals
{
	public static class AmqpClassMethodConnectionLevelConstants
	{
		public const int ConnectionStart = (10 << 16) | 10;

		public const int ConnectionStartOk = (10 << 16) | 11;

		public const int ConnectionTune = (10 << 16) | 30;

		public const int ConnectionOpenOk = (10 << 16) | 41;

		public const int ConnectionClose = (10 << 16) | 50;

		public const int ConnectionCloseOk = (10 << 16) | 51;

		public const int ConnectionBlocked = (10 << 16) | 60;

		public const int ConnectionUnblocked = (10 << 16) | 61;
	}
}