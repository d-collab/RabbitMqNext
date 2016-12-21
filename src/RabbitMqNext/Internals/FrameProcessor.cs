namespace RabbitMqNext.Internals
{
	using System.Threading.Tasks;


	public interface IFrameProcessor
	{
		Task DispatchMethod(ushort channel, int classMethodId);

		void DispatchHeartbeat();
	}
}