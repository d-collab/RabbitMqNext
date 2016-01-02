namespace RabbitMqNext.Internals
{
	internal static class AmqpConstants
	{
		///<summary>(= 1)</summary>
		public const int FrameMethod = 1;
		///<summary>(= 2)</summary>
		public const int FrameHeader = 2;
		///<summary>(= 3)</summary>
		public const int FrameBody = 3;
		///<summary>(= 8)</summary>
		public const int FrameHeartbeat = 8;
		///<summary>(= 4096)</summary>
		public const int FrameMinSize = 4096;
		///<summary>(= 206)</summary>
		public const int FrameEnd = 206;
		///<summary>(= 200)</summary>
		public const int ReplySuccess = 200;
		///<summary>(= 311)</summary>
		public const int ContentTooLarge = 311;
		///<summary>(= 313)</summary>
		public const int NoConsumers = 313;
		///<summary>(= 320)</summary>
		public const int ConnectionForced = 320;
		///<summary>(= 402)</summary>
		public const int InvalidPath = 402;
		///<summary>(= 403)</summary>
		public const int AccessRefused = 403;
		///<summary>(= 404)</summary>
		public const int NotFound = 404;
		///<summary>(= 405)</summary>
		public const int ResourceLocked = 405;
		///<summary>(= 406)</summary>
		public const int PreconditionFailed = 406;
		///<summary>(= 501)</summary>
		public const int FrameError = 501;
		///<summary>(= 502)</summary>
		public const int SyntaxError = 502;
		///<summary>(= 503)</summary>
		public const int CommandInvalid = 503;
		///<summary>(= 504)</summary>
		public const int ChannelError = 504;
		///<summary>(= 505)</summary>
		public const int UnexpectedFrame = 505;
		///<summary>(= 506)</summary>
		public const int ResourceError = 506;
		///<summary>(= 530)</summary>
		public const int NotAllowed = 530;
		///<summary>(= 540)</summary>
		public const int NotImplemented = 540;
		///<summary>(= 541)</summary>
		public const int InternalError = 541;
	}
}