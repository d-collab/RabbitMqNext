namespace RabbitMqNext.Internals
{
	using System;
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

		public static Task ReceiveTaskAsync(this Socket socket, ArraySegment<byte> segment, SocketFlags flags = SocketFlags.None)
		{
			return Task<int>.Factory.FromAsync(
				(buffer, offset_, size, cb, state) => socket.BeginReceive(buffer, offset_, size, flags, cb, state),
				socket.EndReceive, segment.Array, segment.Offset, segment.Count, (object)null);
		}

		public static Task<int> ReceiveTaskAsync(this Socket socket, byte[] buffer, int offset, int length, SocketFlags flags = SocketFlags.None)
		{
			// TODO: timeout
			return Task<int>.Factory.FromAsync(
				(buffer_, offset_, size, cb, state) => socket.BeginReceive(buffer_, offset_, size, flags, cb, state),
				socket.EndReceive, buffer, offset, length, null);
		}

		private static Task<int> SendTaskAsync(this Socket socket, ArraySegment<byte> segment, int offset = 0, SocketFlags flags = SocketFlags.None)
		{
			return Task<int>.Factory.FromAsync(
				(buffer, offset_, size, cb, state) => socket.BeginSend(buffer, offset_, size, flags, cb, state),
				socket.EndSend, segment.Array, segment.Offset + offset, segment.Count, (object)null);
		}

		public static Task<int> SendTaskAsync(this Socket socket, byte[] buffer, int offset, int length, SocketFlags flags = SocketFlags.None)
		{
			// TODO: timeout
			return Task<int>.Factory.FromAsync(
				(buffer_, offset_, size, cb, state) => socket.BeginSend(buffer_, offset_, size, flags, cb, state),
				socket.EndSend, buffer, offset, length, null);
		}

		public static async Task WriteAsync(this Socket socket, ArraySegment<byte> segment)
		{
			int bytesSent = 0;
			while (bytesSent != segment.Count)
			{
				bytesSent += await socket.SendTaskAsync(segment, bytesSent);
			}
		}

		public static async Task WriteAsync(this Socket socket, byte[] buffer, int offset, int count)
		{
			int bytesSent = 0;
			while (bytesSent != count)
			{
				bytesSent += await socket.SendTaskAsync(buffer, offset + bytesSent, count - bytesSent);
			}
		}

		public static void WriteSync(this Socket socket, byte[] buffer, int offset, int count)
		{
			int bytesSent = 0;
			while (bytesSent != count)
			{
				bytesSent += socket.Send(buffer, offset + bytesSent, count - bytesSent, SocketFlags.None);
			}
		}
	}
}