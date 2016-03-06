namespace RabbitMqNext.Internals.Sockets
{
	using System.Net;
	using System.Net.Sockets;
	using System.Threading.Tasks;

	public static class SocketExtensions
	{
		public static Task ConnectTaskAsync(this Socket socket, EndPoint endpoint)
		{
			// TODO: timeout
			return Task.Factory.FromAsync(socket.BeginConnect, socket.EndConnect, endpoint, null);
		}
	}
}