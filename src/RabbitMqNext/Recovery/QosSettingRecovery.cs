namespace RabbitMqNext.Recovery
{
	using System.Threading.Tasks;

	internal struct QosSettingRecovery
	{
		private readonly uint _prefetchSize;
		private readonly ushort _prefetchCount;
		private readonly bool _global;

		public QosSettingRecovery(uint prefetchSize, ushort prefetchCount, bool global)
		{
			_prefetchSize = prefetchSize;
			_prefetchCount = prefetchCount;
			_global = global;
		}

		public Task Apply(Channel channel)
		{
			return channel.BasicQos(_prefetchSize, _prefetchCount, _global);
		}
	}
}