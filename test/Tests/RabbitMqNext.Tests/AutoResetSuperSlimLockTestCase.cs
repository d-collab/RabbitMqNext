namespace RabbitMqNext.Tests
{
	using System;
	using System.Collections.Generic;
	using System.Threading;
	using System.Threading.Tasks;
	using FluentAssertions;
	using Internals.RingBuffer.Locks;
	using NUnit.Framework;

	[TestFixture]
	public class AutoResetSuperSlimLockTestCase
	{
		[Test]
		public void InternalOps()
		{
			var autoResSlim1 = new AutoResetSuperSlimLock(false);

			autoResSlim1.IsSet.Should().BeFalse();
			autoResSlim1.Waiters.Should().Be(0);
			autoResSlim1.Operational.Should().BeTrue();

			autoResSlim1.Waiters = 34;
			autoResSlim1.Waiters.Should().Be(34);

			autoResSlim1.Waiters.Should().Be(34);

			autoResSlim1.TryAtomicXor(1, AutoResetSuperSlimLock.SignalledStatePos, AutoResetSuperSlimLock.SignalledStateMask);

			autoResSlim1.IsSet.Should().BeTrue();
			autoResSlim1.Waiters.Should().Be(34);

			autoResSlim1.TryAtomicXor(0, AutoResetSuperSlimLock.SignalledStatePos, AutoResetSuperSlimLock.SignalledStateMask);
			autoResSlim1.IsSet.Should().BeFalse();

			autoResSlim1.AtomicChange(1, AutoResetSuperSlimLock.SignalledStatePos, AutoResetSuperSlimLock.SignalledStateMask);
			autoResSlim1.IsSet.Should().BeTrue();

			autoResSlim1.AtomicChange(0, AutoResetSuperSlimLock.SignalledStatePos, AutoResetSuperSlimLock.SignalledStateMask);
			autoResSlim1.IsSet.Should().BeFalse();
			autoResSlim1.Waiters.Should().Be(34);
		}

		[Test]
		public void IsSet_ReflectsState()
		{
			var autoResSlim1 = new AutoResetSuperSlimLock(true);
			var autoResSlim2 = new AutoResetSuperSlimLock(false);

			autoResSlim1.IsSet.Should().BeTrue();
			autoResSlim2.IsSet.Should().BeFalse();
		}

		[Test]
		public void Wait_WhenAlreadySet_ReturnsAndResets()
		{
			var autoResSlim = new AutoResetSuperSlimLock(true);

			autoResSlim.Wait().Should().BeTrue();
			autoResSlim.Wait(0).Should().BeFalse();

			autoResSlim.IsSet.Should().BeFalse();
		}

		[Test]
		public void ConcurrentWaits_1()
		{
			var autoResSlim = new AutoResetSuperSlimLock(true);

			var barrier = new ManualResetEvent(false);

			int countOfObtained = 0;
			var tasks = new List<Task>();

			for (int i = 0; i < 4; i++)
			{
				var t = Task.Factory.StartNew(() =>
				{
					barrier.WaitOne();

					if (autoResSlim.Wait(100)) Interlocked.Increment(ref countOfObtained);
				});
				tasks.Add(t);
			}

			barrier.Set();

			Task.WaitAll(tasks.ToArray());

			autoResSlim.IsSet.Should().BeFalse();

			countOfObtained.Should().Be(1);
		}

		[Test]
		public void ConcurrentWaits_2()
		{
			var autoResSlim = new AutoResetSuperSlimLock(false);

			var barrier = new ManualResetEvent(false);

			int countOfObtained = 0;
			var tasks = new List<Task>();
			var _run = true;

			for (int i = 0; i < 4; i++)
			{
				var t = Task.Factory.StartNew(() =>
				{
					barrier.WaitOne();

					while (_run)
					{
						if (autoResSlim.Wait(100))
						{
							Interlocked.Increment(ref countOfObtained);
							break;
						}
					}
				});
				tasks.Add(t);
			}
			
			barrier.Set();

			Thread.Sleep(500);
			
			autoResSlim.Set();
			
			Thread.Sleep(1);
			_run = false;

			Task.WaitAll(tasks.ToArray());

			countOfObtained.Should().Be(1);
			autoResSlim.IsSet.Should().BeFalse();
		}

		[Test]
		public void ConcurrentSetsAndWaits__3()
		{
			var autoResSlim = new AutoResetSuperSlimLock(false);

			var obtainedEv = new AutoResetEvent(true);
			var barrier = new ManualResetEvent(false);
			var semaphore = new SemaphoreSlim(0, 4);

			int countOfObtained = 0;
			int countOfMissed = 0;
			var tasks = new List<Task>();

			for (int i = 0; i < 4; i++)
			{
				var t = Task.Factory.StartNew((p) =>
				{
					barrier.WaitOne();

					for (int j = 0; j < 1000; j++)
					{
						if (autoResSlim.Wait())
						{
							Interlocked.Increment(ref countOfObtained);
//							Console.WriteLine(countOfObtained + " _ " + p);
							obtainedEv.Set();
						}
						else
						{
							Interlocked.Increment(ref countOfMissed);
						}
					}

					semaphore.Release();
				}, i);
				tasks.Add(t);
			}

			var t1 = Task.Factory.StartNew(() =>
			{
				barrier.Set();
				for (int i = 0; i < 4000; i++)
				{
					obtainedEv.WaitOne();
					autoResSlim.Set();
//					Console.WriteLine("*\t" + i);
				}
			});
			tasks.Add(t1);

			Task.WaitAll(tasks.ToArray());

			for (int i = 0; i < 4; i++)
				semaphore.Wait();

			countOfObtained.Should().Be(4000);

			autoResSlim.IsSet.Should().BeFalse();
		}

		[Test]
		public void Reset_WithWaiters()
		{
			var autoResSlim = new AutoResetSuperSlimLock(false);

			int releasedCounter = 0;
			const int totalThreads = 5;

			for (int i = 0; i < totalThreads; i++)
			{
				new Thread(() =>
				{
					autoResSlim.Wait();

					Interlocked.Increment(ref releasedCounter);

				}) { IsBackground = true }.Start();
			}

			Thread.Sleep(2000);

			autoResSlim.Waiters.Should().Be(totalThreads);

			autoResSlim.Reset();

			Thread.Sleep(100);

			autoResSlim.Waiters.Should().Be(0);
			releasedCounter.Should().Be(totalThreads);

			autoResSlim.IsSet.Should().BeFalse();
		}
	}
}