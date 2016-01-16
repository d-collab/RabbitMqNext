namespace RabbitMqNext.Internals.RingBuffer
{
	internal class ReadingGate
	{
		public volatile bool inEffect = true;
		public volatile uint gpos, length;
		public int index;

		public override string ToString()
		{
			return "[ineffect " + inEffect + " gpos "+ gpos+" len " + length + " idx " + index + "]";
		}
	}
}