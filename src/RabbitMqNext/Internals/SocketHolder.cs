namespace RabbitMqNext.Internals
{
	using System;
	using System.Net;
	using System.Net.Sockets;
	using System.Threading;
	using System.Threading.Tasks;
	using Internals.RingBuffer;


	// TODO: Needs lots of unit testing
	// The idea here is to be able to replace the sockets and re-use the ringbuffers. we dont want 
	// to allocate new ones. that said, the cancellation tokens are one time cancel. and we need to break
	// the consumer loops before reutilizing the ring buffers.
	public class SocketHolder
	{
		private readonly ByteRingBuffer _inputBuffer;
		private readonly RingBufferStreamAdapter _inputRingBufferStream;

		private Socket _socket;

		private SocketStreamWriterAdapter _socketConsumer;
//		private SocketProducer _socketProducer;
		private Action _notifyWhenClosed;

		public InternalBigEndianWriter Writer;
		public InternalBigEndianReader Reader;

		internal int _socketIsClosed = 0;
		private int _index;

		public SocketHolder()
		{
			_inputBuffer = new ByteRingBuffer();
			_inputRingBufferStream = new RingBufferStreamAdapter(_inputBuffer);
		}

		public bool IsClosed
		{
			get { return _socketIsClosed != 0; } //&& _outputBuffer.HasUnreadContent; }
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

		public async Task<bool> Connect(string hostname, int port, int index, bool throwOnError)
		{
			_index = index;
			var socket = new Socket(SocketType.Stream, ProtocolType.Tcp);
			socket.SetSocketOption(SocketOptionLevel.Tcp, SocketOptionName.NoDelay, true);


			IPAddress[] addresses;
			try
			{
				addresses = Dns.GetHostAddresses(hostname);
			}
			catch (Exception)
			{
				if (throwOnError) throw;
				return false;
			}

			var started = false;

			foreach (var ipAddress in addresses)
			{
				if (ipAddress.AddressFamily == AddressFamily.InterNetwork)
				{
					started = true;

					try
					{
						var endpoint = new IPEndPoint(ipAddress, port);
						await Task.Factory.FromAsync(socket.BeginConnect, socket.EndConnect, endpoint, null).ConfigureAwait(false);
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

			_socketIsClosed = 0;
			_socket = socket;

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

		internal void WireStreams(CancellationToken cancellationToken, Action notifyWhenClosed)
		{
			_inputBuffer.SetSocket(_socket, OnSocketClosed);

			_inputBuffer.ReenableBuffers();

			_notifyWhenClosed = notifyWhenClosed;

			// WriteLoop
			_socketConsumer = new SocketStreamWriterAdapter(_socket)
			{
				OnNotifyClosed = OnSocketClosed
			};

			// ReadLoop
			// _socketProducer = new SocketProducer(_socket, _inputBuffer, cancellationToken, _index);
			// _socketProducer.OnNotifyClosed += OnSocketClosed;
			Writer = new InternalBigEndianWriter(_socketConsumer.Write);
			Reader = new InternalBigEndianReader(_inputRingBufferStream);
			// Reader = new InternalBigEndianReader(new SocketStreamReaderAdapter(_socket));
		}

		private void OnSocketClosed(Socket sender, Exception ex)
		{
			if (Interlocked.CompareExchange(ref _socketIsClosed, 1, 0) == 0)
			{
				this._notifyWhenClosed();
			}
		}

		internal void StopAndBlockBuffers()
		{
			_inputBuffer.StopAndBlockBuffers();
		}
	}
}