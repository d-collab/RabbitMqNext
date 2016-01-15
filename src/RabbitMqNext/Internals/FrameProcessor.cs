namespace RabbitMqNext.Internals
{
	using System.Threading.Tasks;


	public interface IFrameProcessor
	{
		// internal abstract Task DispatchMethod(ushort channel, int classMethodId);
		Task DispatchMethod(ushort channel, int classMethodId);

//		public abstract Task DispatchCloseMethod(ushort channel, ushort replyCode, string replyText, ushort classId, ushort methodId);
//
//		public abstract Task DispatchChannelCloseMethod(ushort channel, ushort replyCode, string replyText, ushort classId, ushort methodId);
	}
}