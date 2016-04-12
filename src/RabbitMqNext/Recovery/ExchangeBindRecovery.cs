namespace RabbitMqNext.Recovery
{
	using System;
	using System.Collections.Generic;
	using System.Threading.Tasks;

	internal class ExchangeBindRecovery
	{
		private readonly string _source;
		private readonly string _destination;
		private readonly string _routingKey;
		private readonly IDictionary<string, object> _arguments;

		public ExchangeBindRecovery(string source, string destination, string routingKey, IDictionary<string, object> arguments)
		{
			_source = source;
			_destination = destination;
			_routingKey = routingKey;
			_arguments = arguments;
		}

		public Task Apply(Channel channel)
		{
			if (LogAdapter.ExtendedLogEnabled)
				LogAdapter.LogDebug("Recovery", "Recovering binding source: " + _source + " dest: " + _destination + " routing: " + _routingKey);

			return channel.ExchangeBind(_source, _destination, _routingKey, _arguments, waitConfirmation: true);
		}

		protected bool Equals(ExchangeBindRecovery other)
		{
			return StringComparer.Ordinal.Equals(_source, other._source) &&
				   StringComparer.Ordinal.Equals(_destination, other._destination) &&
				   StringComparer.Ordinal.Equals(_routingKey, other._routingKey);
		}

		public override bool Equals(object obj)
		{
			if (ReferenceEquals(null, obj)) return false;
			if (ReferenceEquals(this, obj)) return true;
			if (obj.GetType() != this.GetType()) return false;
			return Equals((ExchangeBindRecovery) obj);
		}

		public override int GetHashCode()
		{
			unchecked
			{
				var hashCode = (_source != null ? _source.GetHashCode() : 0);
				hashCode = (hashCode*397) ^ (_destination != null ? _destination.GetHashCode() : 0);
				hashCode = (hashCode*397) ^ (_routingKey != null ? _routingKey.GetHashCode() : 0);
				return hashCode;
			}
		}
	}
}