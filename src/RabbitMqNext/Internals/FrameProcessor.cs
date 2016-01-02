namespace RabbitMqNext.Internals
{
	using System.Threading.Tasks;


	public abstract class FrameProcessor
	{
		public abstract Task DispatchMethod(ushort channel, int classMethodId);
	}
}