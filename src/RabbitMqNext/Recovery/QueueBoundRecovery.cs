namespace RabbitMqNext.Recovery
{
	using System;
	using System.Collections.Generic;
	using System.Threading.Tasks;

	internal class QueueBoundRecovery
	{
		private readonly string _queue;
		private readonly string _exchange;
		private readonly string _routingKey;
		private readonly IDictionary<string, object> _arguments;

		public QueueBoundRecovery(string queue, string exchange, string routingKey, IDictionary<string, object> arguments)
		{
			_queue = queue;
			_exchange = exchange;
			_routingKey = routingKey;
			_arguments = arguments;
		}

		public Task Apply(Channel channel)
		{
			if (LogAdapter.ExtendedLogEnabled)
				LogAdapter.LogDebug("Recovery", "Recovering binding queue: " + _queue + " ex: " + _exchange + " routing: " + _routingKey);

			return channel.QueueBind(_queue, _exchange, _routingKey, _arguments, waitConfirmation: true);
		}

		protected bool Equals(QueueBoundRecovery other)
		{
			return StringComparer.Ordinal.Equals(_queue, other._queue) &&
				   StringComparer.Ordinal.Equals(_exchange, other._exchange) &&
				   StringComparer.Ordinal.Equals(_routingKey, other._routingKey);
		}

		public override bool Equals(object obj)
		{
			if (ReferenceEquals(null, obj)) return false;
			if (ReferenceEquals(this, obj)) return true;
			if (obj.GetType() != this.GetType()) return false;
			return Equals((QueueBoundRecovery) obj);
		}

		public override int GetHashCode()
		{
			unchecked
			{
				var hashCode = (_queue != null ? _queue.GetHashCode() : 0);
				hashCode = (hashCode*397) ^ (_exchange != null ? _exchange.GetHashCode() : 0);
				hashCode = (hashCode*397) ^ (_routingKey != null ? _routingKey.GetHashCode() : 0);
				return hashCode;
			}
		}
	}
}