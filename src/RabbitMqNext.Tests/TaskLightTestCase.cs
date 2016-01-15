namespace RabbitMqNext.Tests
{
	using System;
	using FluentAssertions;
	using NUnit.Framework;

	[TestFixture]
	public class TaskLightTestCase
	{
		[Test]
		public void SetCompleted_Should_setCompletedFlags()
		{
			var taskLight = new TaskLight(null);

			taskLight.IsCompleted.Should().BeFalse();
			taskLight.HasContinuation.Should().BeFalse();
			taskLight.HasException.Should().BeFalse();
			taskLight.RunContinuationAsync.Should().BeFalse();

			taskLight.SetCompleted(runContinuationAsync: false);

			taskLight.IsCompleted.Should().BeTrue();
			taskLight.HasContinuation.Should().BeFalse();
			taskLight.HasException.Should().BeFalse();
			taskLight.RunContinuationAsync.Should().BeFalse();
		}

		[Test]
		public void SetCompleted_Should_setCompletedFlags2()
		{
			var taskLight = new TaskLight(null);

			taskLight.IsCompleted.Should().BeFalse();
			taskLight.HasContinuation.Should().BeFalse();
			taskLight.HasException.Should().BeFalse();
			taskLight.RunContinuationAsync.Should().BeFalse();

			taskLight.SetCompleted(runContinuationAsync: true);

			taskLight.IsCompleted.Should().BeTrue();
			taskLight.HasContinuation.Should().BeFalse();
			taskLight.HasException.Should().BeFalse();
			taskLight.RunContinuationAsync.Should().BeTrue();
		}

		[Test]
		public void OnCompleted_Should_SetHasContinuationFlag()
		{
			var taskLight = new TaskLight(null);

			taskLight.IsCompleted.Should().BeFalse();
			taskLight.HasContinuation.Should().BeFalse();
			taskLight.HasException.Should().BeFalse();
			taskLight.RunContinuationAsync.Should().BeFalse();

			taskLight.OnCompleted(() =>
			{
			});

			taskLight.IsCompleted.Should().BeFalse();
			taskLight.HasContinuation.Should().BeTrue();
			taskLight.HasException.Should().BeFalse();
			taskLight.RunContinuationAsync.Should().BeFalse();
		}

		[Test]
		public void SetComplete_Should_RunContinuationIfOneIsPresent()
		{
			var taskLight = new TaskLight(null);

			taskLight.IsCompleted.Should().BeFalse();
			taskLight.HasContinuation.Should().BeFalse();
			taskLight.HasException.Should().BeFalse();
			taskLight.RunContinuationAsync.Should().BeFalse();

			var runCont = false;
			taskLight.OnCompleted(() =>
			{
				runCont = true;
			});

			taskLight.SetCompleted(runContinuationAsync: false);

			runCont.Should().BeTrue();
		}

		[Test]
		public void OnCompleted_Should_RunContinuationIfCompleted()
		{
			var taskLight = new TaskLight(null);

			taskLight.IsCompleted.Should().BeFalse();
			taskLight.HasContinuation.Should().BeFalse();
			taskLight.HasException.Should().BeFalse();
			taskLight.RunContinuationAsync.Should().BeFalse();

			taskLight.SetCompleted(runContinuationAsync: false);

			var runCont = false;
			taskLight.OnCompleted(() =>
			{
				runCont = true;
				taskLight.GetResult();
			});

			runCont.Should().BeTrue();
		}

		[Test]
		public void OnCompleted_Should_RunContinuationIfCompletedWithException()
		{
			var taskLight = new TaskLight(null);

			taskLight.IsCompleted.Should().BeFalse();
			taskLight.HasContinuation.Should().BeFalse();
			taskLight.HasException.Should().BeFalse();
			taskLight.RunContinuationAsync.Should().BeFalse();

			taskLight.SetException(new Exception("nope"), runContinuationAsync: false);

			var runCont = false;
			taskLight.OnCompleted(() =>
			{
				runCont = true;

				Assert.Throws<Exception>(() =>
				{
					taskLight.GetResult(); // throws the exception
				});
			});

			runCont.Should().BeTrue();
		}

		[Test]
		public void SetException_Should_RunContinuationIfOneIsPresent()
		{
			var taskLight = new TaskLight(null);

			taskLight.IsCompleted.Should().BeFalse();
			taskLight.HasContinuation.Should().BeFalse();
			taskLight.HasException.Should().BeFalse();
			taskLight.RunContinuationAsync.Should().BeFalse();

			var runCont = false;
			taskLight.OnCompleted(() =>
			{
				runCont = true;

				Assert.Throws<Exception>(() =>
				{
					taskLight.GetResult(); // throws the exception
				});
			});

			taskLight.SetException(new Exception("nope"), runContinuationAsync: false);

			runCont.Should().BeTrue();
		}
	}
}
