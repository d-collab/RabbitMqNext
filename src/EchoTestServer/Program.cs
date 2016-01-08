namespace EchoTestServer
{
	using System;
	using System.Net;
	using System.Net.Sockets;
	using System.Runtime;
	using System.Threading;

	class Program
	{
		private static Random _rnd = new Random();

		const bool SimulateInstability = false;

		private static AutoResetEvent _event = new AutoResetEvent(false);
		private static AutoResetEvent _acceptNext;

		static void Main(string[] args)
		{
			AppDomain.CurrentDomain.UnhandledException += (sender, eventArgs) =>
			{
				Console.WriteLine("wtf? " + eventArgs.ExceptionObject.ToString());
			};
			AppDomain.CurrentDomain.FirstChanceException += (sender, eventArgs) =>
			{
				Console.WriteLine("ops " + eventArgs.Exception.ToString());
			};

//			Console.WriteLine("Is Server GC: " + GCSettings.IsServerGC);
//			GCSettings.LargeObjectHeapCompactionMode = GCLargeObjectHeapCompactionMode.CompactOnce;
//			Console.WriteLine("Compaction mode: " + GCSettings.LargeObjectHeapCompactionMode);
//			Console.WriteLine("Latency mode: " + GCSettings.LatencyMode);
//			GCSettings.LatencyMode = GCLatencyMode.SustainedLowLatency;
//			Console.WriteLine("New Latency mode: " + GCSettings.LatencyMode);


			var socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);

			socket.Bind(new IPEndPoint(IPAddress.Any, 6767));
			socket.Listen(1000);

			var shouldGoOn = true;
			Console.CancelKeyPress += (sender, eventArgs) =>
			{
				Console.WriteLine("Stopping...");
				shouldGoOn = false;
				_event.Set();
			};

			Console.WriteLine("\n\r\t Server Started at 6767");

			_acceptNext = new AutoResetEvent(false);

			while (shouldGoOn)
			{
				using (var ev = new SocketAsyncEventArgs())
				{
					ev.Completed += OnCompleted;

					if (!socket.AcceptAsync(ev))
					{
						OnCompleted(null, ev);
					}
					else
					{
						_acceptNext.WaitOne();
					}
				}
			}

			_event.WaitOne();

			Console.WriteLine("Goodbye");
		}

		private static void OnCompleted(object sender, SocketAsyncEventArgs socketAsyncEventArgs)
		{
			_acceptNext.Set();

			var newSocket = socketAsyncEventArgs.AcceptSocket;
			Console.WriteLine("Accepted connection from  " + newSocket.RemoteEndPoint);

			ThreadPool.UnsafeQueueUserWorkItem(EchoLoop, newSocket);
		}

		private static void EchoLoop(object state)
		{
			try
			{
				var socket = (Socket) state;
				socket.SetSocketOption(SocketOptionLevel.Tcp, SocketOptionName.NoDelay, true);

				const int BufferLen = 0xF100000;
				var temp = new byte[BufferLen];

				while (socket.Connected)
				{
					try
					{
						var read = socket.Receive(temp, 0, BufferLen, SocketFlags.None);
						if (read > 0)
						{
	//						Console.WriteLine("Recv " );
	//						for (int i = 0; i < read; i++)
	//						{
	//							Console.Write(temp[i] + ", ");
	//						}
	//						Console.WriteLine("");

	//						if (SimulateInstability)
	//							Thread.Sleep(_rnd.Next(10));

							var written = 0;
							while (written < read)
							{
								written += socket.Send(temp, written, read - written, SocketFlags.None);
							}
						}
					}
					catch (Exception ex)
					{
						Console.WriteLine("Error  " + ex);

						socket.Close();
					}
				}
			}
			catch (Exception ex)
			{
				Console.WriteLine("EchoLoop " + ex);
				throw;
			}
		}
	}
}
