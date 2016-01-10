namespace RabbitMqNext.Tests
{
	using System;
	using System.Net;
	using System.Net.Sockets;
	using System.Threading;
	using FluentAssertions;
	using Internals.RingBuffer;
	using NUnit.Framework;

	[TestFixture]
	public class RingBuffer2_SocketIntegration_TestCase
	{
		[Test]
		public void WritesOf31ReadsOf31_BufferOf32()
		{
			var autoResEv = new AutoResetEvent(false);

			Socket acceptedSocket = null;
			var socketOut = new Socket(SocketType.Stream, ProtocolType.Tcp);
			var socketAccept = new Socket(SocketType.Stream, ProtocolType.Tcp);
			socketAccept.Bind(new IPEndPoint(IPAddress.Any, 6868));

			var socketEvArgs = new SocketAsyncEventArgs();
			socketEvArgs.Completed += (sender, args) =>
			{
				acceptedSocket = args.AcceptSocket;
				autoResEv.Set();
			};
			socketAccept.Listen(1);
			if (!socketAccept.AcceptAsync(socketEvArgs))
			{
				acceptedSocket = socketEvArgs.AcceptSocket;
			}

			socketOut.SetSocketOption(SocketOptionLevel.Tcp, SocketOptionName.NoDelay, true);
			socketOut.Connect(new IPEndPoint(IPAddress.Loopback, 6868));

			autoResEv.WaitOne();

			acceptedSocket.SetSocketOption(SocketOptionLevel.Tcp, SocketOptionName.NoDelay, true);

			var cancellationTokenSrc = new CancellationTokenSource();
			var cancellationToken = cancellationTokenSrc.Token;

			var inputBuffer = new BufferRingBuffer(cancellationToken);
			var outputBuffer = new BufferRingBuffer(cancellationToken);

			var inputRingBufferStream = new RingBufferStreamAdapter(inputBuffer);
			var outputRingBufferStream = new RingBufferStreamAdapter(outputBuffer);

			// WriteLoop
			var socketConsumer = new SocketConsumer(socketOut, outputBuffer, cancellationToken);

			// ReadLoop
			var socketProducer = new SocketProducer(acceptedSocket, inputBuffer, cancellationToken);


			var input = new byte[1025];
			var output = new byte[1025];
			for (int j = 0; j < input.Length; j++)
			{
				input[j] = (byte)(j % 256);
			}

			for (ulong i = 0L; i < 1000000; i++)
			{
				outputRingBufferStream.Write(input, 0, input.Length);

				var read = inputRingBufferStream.Read(output, 0, output.Length);
				read.Should().Be(output.Length);

				for (int x = 0; x < output.Length; x++)
				{
					output[x].Should().Be((byte)(x % 256), "Iteration " + i + " pos " + x);
				}

				if (i % 10000 == 0)
				{
					Console.WriteLine("Iteration " + i);
				}
			}

			cancellationTokenSrc.Cancel();
			socketOut.Close();
			socketAccept.Close();
		}
	}
}