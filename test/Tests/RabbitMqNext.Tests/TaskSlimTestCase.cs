namespace RabbitMqNext.Tests
{
	using System;
	using System.ComponentModel;
	using System.Threading;
	using System.Threading.Tasks;
	using FluentAssertions;
	using Internals.RingBuffer;
	using NUnit.Framework;


	/// <summary>
	/// missing test for run async
	/// </summary>
	[TestFixture]
	public class TaskSlimTestCase
	{
		[Test]
		public void SetCompleted_Should_setCompletedFlags()
		{
			var taskSlim = new TaskSlim(null);

			taskSlim.IsCompleted.Should().BeFalse();
			taskSlim.HasContinuation.Should().BeFalse();
			taskSlim.HasException.Should().BeFalse();
			taskSlim.RunContinuationAsync.Should().BeFalse();

			taskSlim.SetCompleted(runContinuationAsync: false);

			taskSlim.IsCompleted.Should().BeTrue();
			taskSlim.HasContinuation.Should().BeFalse();
			taskSlim.HasException.Should().BeFalse();
			taskSlim.RunContinuationAsync.Should().BeFalse();
		}

		[Test]
		public void SetCompleted_Should_setCompletedFlags2()
		{
			var taskSlim = new TaskSlim(null);

			taskSlim.IsCompleted.Should().BeFalse();
			taskSlim.HasContinuation.Should().BeFalse();
			taskSlim.HasException.Should().BeFalse();
			taskSlim.RunContinuationAsync.Should().BeFalse();

			taskSlim.SetCompleted(runContinuationAsync: true);

			taskSlim.IsCompleted.Should().BeTrue();
			taskSlim.HasContinuation.Should().BeFalse();
			taskSlim.HasException.Should().BeFalse();
			taskSlim.RunContinuationAsync.Should().BeTrue();
		}

		[Test]
		public void OnCompleted_Should_SetHasContinuationFlag()
		{
			var taskSlim = new TaskSlim(null);

			taskSlim.IsCompleted.Should().BeFalse();
			taskSlim.HasContinuation.Should().BeFalse();
			taskSlim.HasException.Should().BeFalse();
			taskSlim.RunContinuationAsync.Should().BeFalse();

			taskSlim.OnCompleted(() =>
			{
			});

			taskSlim.IsCompleted.Should().BeFalse();
			taskSlim.HasContinuation.Should().BeTrue();
			taskSlim.HasException.Should().BeFalse();
			taskSlim.RunContinuationAsync.Should().BeFalse();
		}

		[Test]
		public void SetComplete_Should_RunContinuationIfOneIsPresent()
		{
			var taskSlim = new TaskSlim(null);

			taskSlim.IsCompleted.Should().BeFalse();
			taskSlim.HasContinuation.Should().BeFalse();
			taskSlim.HasException.Should().BeFalse();
			taskSlim.RunContinuationAsync.Should().BeFalse();

			var runCont = false;
			taskSlim.OnCompleted(() =>
			{
				runCont = true;
			});

			taskSlim.SetCompleted(runContinuationAsync: false);

			runCont.Should().BeTrue();
		}

		[Test]
		public void OnCompleted_Should_RunContinuationIfCompleted()
		{
			var taskSlim = new TaskSlim(null);

			taskSlim.IsCompleted.Should().BeFalse();
			taskSlim.HasContinuation.Should().BeFalse();
			taskSlim.HasException.Should().BeFalse();
			taskSlim.RunContinuationAsync.Should().BeFalse();

			taskSlim.SetCompleted(runContinuationAsync: false);

			var runCont = false;
			taskSlim.OnCompleted(() =>
			{
				runCont = true;
				taskSlim.GetResult();
			});

			runCont.Should().BeTrue();
		}

		[Test]
		public void OnCompleted_Should_RunContinuationIfCompletedWithException()
		{
			var taskSlim = new TaskSlim(null);

			taskSlim.IsCompleted.Should().BeFalse();
			taskSlim.HasContinuation.Should().BeFalse();
			taskSlim.HasException.Should().BeFalse();
			taskSlim.RunContinuationAsync.Should().BeFalse();

			taskSlim.SetException(new Exception("nope"), runContinuationAsync: false);

			var runCont = false;
			taskSlim.OnCompleted(() =>
			{
				runCont = true;

				Assert.Throws<Exception>(() =>
				{
					taskSlim.GetResult(); // throws the exception
				});
			});

			runCont.Should().BeTrue();
		}

		[Test]
		public void SetException_Should_RunContinuationIfOneIsPresent()
		{
			var taskSlim = new TaskSlim(null);

			taskSlim.IsCompleted.Should().BeFalse();
			taskSlim.HasContinuation.Should().BeFalse();
			taskSlim.HasException.Should().BeFalse();
			taskSlim.RunContinuationAsync.Should().BeFalse();

			var runCont = false;
			taskSlim.OnCompleted(() =>
			{
				runCont = true;

				Assert.Throws<Exception>(() =>
				{
					taskSlim.GetResult(); // throws the exception
				});
			});

			taskSlim.SetException(new Exception("nope"), runContinuationAsync: false);

			runCont.Should().BeTrue();
		}

		[TestCase(true, 1)]
		[TestCase(true, 2)]
		[TestCase(true, 3)]
		[TestCase(true, 4)]
		[TestCase(true, 5)]
		[TestCase(true, 6)]
		[TestCase(true, 7)]
		[TestCase(true, 8)]
		[TestCase(false, 1)]
		[TestCase(false, 2)]
		[TestCase(false, 3)]
		[TestCase(false, 4)]
		[TestCase(false, 5)]
		[TestCase(false, 6)]
		[TestCase(false, 7)]
		[TestCase(false, 8)]
		public void MultiThreaded_TrySetComplete(bool isSetCompleteTest, int howManyThreads)
		{
			int continuationCalledCounter = 0;
			int completedGainedCounter = 0;
			int completedLossedCounter = 0;

			var taskSlim = new TaskSlim(null);
			taskSlim.SetContinuation(() =>
			{
				Interlocked.Increment(ref continuationCalledCounter);
			});

			var @syncEvent = new ManualResetEventSlim(false);

			for (int i = 0; i < howManyThreads; i++)
			{
				ThreadFactory.BackgroundThread((index) =>
				{
					@syncEvent.Wait();

					var result = isSetCompleteTest ? 
						taskSlim.TrySetCompleted() : 
						taskSlim.TrySetException(new Exception("bah"));

					if (result)
						Interlocked.Increment(ref completedGainedCounter);
					else
						Interlocked.Increment(ref completedLossedCounter);

				}, "T_" + i, i);
			}

			Thread.Sleep(0);

			@syncEvent.Set();

			Thread.Sleep(1000);

			continuationCalledCounter.Should().Be(1, "continuation cannot be called more than once");
			completedGainedCounter.Should().Be(1, "only 1 thread could have taken the lock successfully");
			completedLossedCounter.Should().Be(howManyThreads - 1, "all other threads should have been unsuccessful");
		}
	}
}
