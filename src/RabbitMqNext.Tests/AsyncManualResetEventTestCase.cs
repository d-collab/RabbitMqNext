namespace RabbitMqNext.Tests
{
	using System;
	using System.Diagnostics;
	using System.Threading.Tasks;
	using Internals;
	using NUnit.Framework;

	[TestFixture]
	public class AsyncManualResetEventTestCase
	{
		[Test]
		public async Task Basic2()
		{
			var ev = new AsyncManualResetEvent();

			Task.Run(async () =>
			{
				await Task.Delay(5);
				ev.Set();
			});

			var watch = Stopwatch.StartNew();
			await ev.WaitAsync();
			watch.Stop();

			Console.WriteLine("[Basic2] Done took " + watch.Elapsed.TotalMilliseconds + "ms");
		}

		[Test]
		public async Task InverseOrder()
		{
//			var semSLim = new SemaphoreSlim(1,1);
			var ev = new AsyncManualResetEvent();
			ev.Set();
			await ev.WaitAsync();

//			var ev1 = new ManualResetEventSlim();
			var watch = Stopwatch.StartNew();
//			ev1.Set();
			ev.Set();
			await ev.WaitAsync();
//			await semSLim.WaitAsync();
//			ev1.Wait();
			watch.Stop();

			Console.WriteLine("[InverseOrder] Done took " + watch.Elapsed.TotalMilliseconds + "ms");
		}

		[Test]
		public async Task BasicUsage()
		{
			var ev = new AsyncManualResetEvent(/*false*/);

			var watch = Stopwatch.StartNew();

			var producerTask = Task.Run(async () =>
			{
				await Task.Delay(2000);
				ev.Set2();
			});

			await ev.WaitAsync();
			watch.Stop();

			Console.WriteLine("[BasicUsage] Done took " + watch.Elapsed.TotalMilliseconds + "ms");
		}
	}
}