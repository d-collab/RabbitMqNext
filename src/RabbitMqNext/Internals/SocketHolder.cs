namespace RabbitMqNext.Internals
{
	using System;
	using System.Net;
	using System.Net.Sockets;
	using System.Threading;
	using System.Threading.Tasks;
	using Internals;
	using Internals.RingBuffer;
	using Sockets;


	// TODO: Needs lots of unit testing
	// The idea here is to be able to replace the sockets and re-use the ringbuffers. we dont want 
	// to allocate new ones. that said, the cancellation tokens are one time one. and we need to break
	// the consumer loops before reutilizing the ring buffers.
	public class SocketHolder
	{
		private readonly CancellationToken _token;
		private readonly ByteRingBuffer _inputBuffer;
		private readonly RingBufferStreamAdapter _inputRingBufferStream; 

		private Socket _socket;

		private SocketStreamWriterAdapter _socketConsumer;
		private SocketProducer _socketProducer;
		private Action _notifyWhenClosed;

		public InternalBigEndianWriter Writer;
		public InternalBigEndianReader Reader;

		private int _socketIsClosed = 0;
		private int _index;

		public SocketHolder(CancellationToken token)
		{
			_token = token;

			_inputBuffer = new ByteRingBuffer();

			_inputRingBufferStream = new RingBufferStreamAdapter(_inputBuffer);
		}

		public bool IsClosed
		{
			get { return _socketIsClosed == 1; } //&& _outputBuffer.HasUnreadContent; }
		}

		// Should be called on termination, no chance of reusing it afterwards
		public void Dispose()
		{
			_inputRingBufferStream.Dispose();

			if (_socket != null)
			{
				_socket.Dispose();
				_socket = null;
			}
		}

		public async Task<bool> Connect(string hostname, int port, Action notifyWhenClosed, int index, bool throwOnError)
		{
			_index = index;
			var socket = new Socket(SocketType.Stream, ProtocolType.Tcp);
			var addresses = Dns.GetHostAddresses(hostname);
			var started = false;

			foreach (var ipAddress in addresses)
			{
				if (ipAddress.AddressFamily == AddressFamily.InterNetwork)
				{
					started = true;
					try
					{
						await socket.ConnectTaskAsync(new IPEndPoint(ipAddress, port)).ConfigureAwait(false);
					}
					catch (Exception)
					{
						socket.Dispose();

						if (throwOnError) throw;

						return false;
					}
					break;
				}
			}

			if (!started)
			{
				if (throwOnError)
				{	
					throw new Exception("Invalid hostname " + hostname); // ipv6 not supported yet
				}
				return false;
			}

			WireStreams(socket, notifyWhenClosed);

			return true;
		}

		public void Close()
		{
			if (_socket != null && _socket.Connected)
			{
				_socket.Close();
				_socket = null;
			}
		}

		private void WireStreams(Socket newSocket, Action notifyWhenClosed)
		{
			if (_socket != null) // reset
			{
				_socketIsClosed = 0; 

				// TODO: replace cancellation token in ringbuffers

				// Writer / Reader.Dipose()
				// _socketConsumer.Dispose();
				// _socketProducer.Dispose();
				// _inputBuffer.Reset();
				// _outputBuffer.Reset();
			}

			_socket = newSocket;
			_notifyWhenClosed = notifyWhenClosed;

			// WriteLoop
			_socketConsumer = new SocketStreamWriterAdapter(_socket);
			_socketConsumer.OnNotifyClosed += OnSocketClosed;

			// ReadLoop
			_socketProducer = new SocketProducer(_socket, _inputBuffer, _token, _index);
			_socketProducer.OnNotifyClosed += OnSocketClosed;

			Writer = new InternalBigEndianWriter(_socketConsumer);
			Reader = new InternalBigEndianReader(_inputRingBufferStream);
		}

		private void OnSocketClosed(Socket sender, Exception ex)
		{
			if (Interlocked.CompareExchange(ref _socketIsClosed, 1, 0) == 0)
			{
				this._notifyWhenClosed();
			}
		}
	}
}