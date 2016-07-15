namespace PerfBuffers
{
	using System;
	using System.Text;
	using BenchmarkDotNet.Attributes;
	using BenchmarkDotNet.Running;
	using RabbitMqNext.Internals;

	public class Program
	{
		private static byte[] _dest = new byte[2048];
		private static byte[] _src = new byte[2048];

		private const int BufferSizeToCopy = 250;

		public Program()
		{
			var quotes =
				"Reason is, and ought only to be the slave of the passions, and can never pretend to any other office than to serve and obey them. " +
				"Nothing is more surprising than the easiness with which the many are governed by the few. " + "" +
				"He is happy whom circumstances suit his temper; but he Is more excellent who suits his temper to any circumstance. ";
			int written = Encoding.UTF8.GetBytes(quotes, 0, quotes.Length, _src, 0);
		}

		static void Main(string[] args)
		{
			BenchmarkRunner.Run<Program>();
		}

		[Benchmark(Baseline = true)]
		public void WithBlockCopy()
		{
			Buffer.BlockCopy(_src, 0, _dest, 0, BufferSizeToCopy);
		}

		[Benchmark]
		public void FastBufferCopy()
		{
			BufferUtil.FastCopy(_dest, 0, _src, 0, BufferSizeToCopy);
		}
	}
}
