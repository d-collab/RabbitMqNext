namespace RabbitMqNext.Tests
{
	using System;
	using FluentAssertions;
	using NUnit.Framework;

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
	}
}
