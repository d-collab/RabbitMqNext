namespace RabbitMqNext
{
	/// <summary>
	/// Structure holding an AMQP timestamp, a posix 64-bit time_t.</summary>
	/// <remarks>
	/// <para>
	/// When converting between an AmqpTimestamp and a System.DateTime,
	/// be aware of the effect of your local timezone. In particular,
	/// different versions of the .NET framework assume different
	/// defaults.
	/// </para>
	/// <para>
	/// We have chosen a signed 64-bit time_t here, since the AMQP
	/// specification through versions 0-9 is silent on whether
	/// timestamps are signed or unsigned.
	/// </para>
	/// </remarks>
	public struct AmqpTimestamp
	{
		/// <summary>
		/// Construct an <see cref="AmqpTimestamp"/>.
		/// </summary>
		/// <param name="unixTime">Unix time.</param>
		public AmqpTimestamp(long unixTime)
			: this()
		{
			UnixTime = unixTime;
		}

		/// <summary>
		/// Unix time.
		/// </summary>
		public long UnixTime { get; private set; }

		/// <summary>
		/// Provides a debugger-friendly display.
		/// </summary>
		public override string ToString()
		{
			return "((time_t)" + UnixTime + ")";
		}
	}
}