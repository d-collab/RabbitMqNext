namespace RabbitMqNext.Internals
{
	using System.Threading.Tasks;


	public abstract class FrameProcessor
	{
		public abstract Task DispatchMethod(ushort channel, int classMethodId);

		public abstract Task DispatchCloseMethod(ushort channel, ushort replyCode, string replyText, ushort classId, ushort methodId);
	}
}