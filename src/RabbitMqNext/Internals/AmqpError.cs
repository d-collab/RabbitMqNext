namespace RabbitMqNext.Internals
{
	public class AmqpError
	{
		public ushort ReplyCode;
		public string ReplyText;
		public ushort ClassId;
		public ushort MethodId;

		public string ToErrorString()
		{
			return "Server returned error: " + ReplyText +
				   " [code: " + ReplyCode + " class: " + ClassId + " method: " + MethodId + "]";
		}
	}
}