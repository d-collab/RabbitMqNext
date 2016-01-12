namespace RabbitMqNext.Internals
{
	using System;
	using System.Net.Sockets;
	using System.Threading;
	using RingBuffer;

	public class SocketRingBuffers : IDisposable
	{
		internal readonly RingBufferStreamAdapter _inputRingBufferStream;
		internal readonly RingBufferStreamAdapter _outputRingBufferStream;

		public readonly InternalBigEndianWriter Writer;
		public readonly InternalBigEndianReader Reader;

		private readonly ByteRingBuffer _inputBuffer, _outputBuffer;
		private readonly SocketConsumer _socketConsumer;
		private readonly SocketProducer _socketProducer;

		private readonly Action _notifyWhenClosed;

		private int _socketIsClosed = 0;

		public SocketRingBuffers(Socket socket, CancellationToken cancellationToken, Action notifyWhenClosed, Action flushWrite)
		{
			_notifyWhenClosed = notifyWhenClosed;

			_inputBuffer = new ByteRingBuffer(cancellationToken);
			_outputBuffer = new ByteRingBuffer(cancellationToken);

			_inputRingBufferStream = new RingBufferStreamAdapter(_inputBuffer);
			_outputRingBufferStream = new RingBufferStreamAdapter(_outputBuffer);

			// WriteLoop
			_socketConsumer = new SocketConsumer(socket, _outputBuffer, cancellationToken, flushWrite);
			_socketConsumer.OnNotifyClosed += OnSocketClosed;

			// ReadLoop
			_socketProducer = new SocketProducer(socket, _inputBuffer, cancellationToken);
			_socketProducer.OnNotifyClosed += OnSocketClosed;

			Writer = new InternalBigEndianWriter(_outputRingBufferStream);
			Reader = new InternalBigEndianReader(_inputRingBufferStream);
		}

		public void Start()
		{
			_socketConsumer.Start();
		}

		private void OnSocketClosed(Socket arg1, Exception arg2)
		{
			if (Interlocked.CompareExchange(ref _socketIsClosed, 1, 0) == 0)
			{
				this._notifyWhenClosed();
			}
		}

		public bool StillSending
		{
			get { return _outputBuffer.HasUnreadContent; }
		}

		public void Dispose()
		{
			_inputRingBufferStream.Dispose();
			_outputRingBufferStream.Dispose();
		}
	}
}