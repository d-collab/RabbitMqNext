namespace RabbitMqNext.Recovery
{
	using System;
	using System.Collections.Generic;
	using System.Threading.Tasks;
	using Internals;

	internal class QueueDeclaredRecovery
	{
		private readonly string _queue;
		private readonly bool _passive;
		private readonly bool _durable;
		private readonly bool _exclusive;
		private readonly bool _autoDelete;
		private readonly IDictionary<string, object> _arguments;

		public QueueDeclaredRecovery(string queue, bool passive, bool durable, bool exclusive, bool autoDelete, IDictionary<string, object> arguments)
			: this(queue)
		{
			_passive = passive;
			_durable = durable;
			_exclusive = exclusive;
			_autoDelete = autoDelete;
			_arguments = arguments;
		}

		public QueueDeclaredRecovery(string queue)
		{
			_queue = queue;
		}

		public async Task Apply(Channel channel, IDictionary<string,string> reservedNamesMapping)
		{
			var queueNameToUse = 
				_queue.StartsWith(AmqpConstants.AmqpReservedPrefix, StringComparison.Ordinal) ? string.Empty : _queue;

			if (LogAdapter.ExtendedLogEnabled)
				LogAdapter.LogDebug("Recovery", "Recovering queue " + _queue);

			var queueDeclareResult = await channel.QueueDeclare(queueNameToUse, _passive, _durable, _exclusive, _autoDelete, _arguments, waitConfirmation: !_passive)
				.ConfigureAwait(false);

			if (queueDeclareResult.Name.StartsWith(AmqpConstants.AmqpReservedPrefix, StringComparison.Ordinal))
			{
				// Special name: let's map the old to the new name. This needs to cascade to binds and consumers
				reservedNamesMapping[_queue] = queueDeclareResult.Name;
			}
		}

		protected bool Equals(QueueDeclaredRecovery other)
		{
			return StringComparer.Ordinal.Equals(_queue, other._queue);
		}

		public override bool Equals(object obj)
		{
			if (ReferenceEquals(null, obj)) return false;
			if (ReferenceEquals(this, obj)) return true;
			if (obj.GetType() != this.GetType()) return false;
			return Equals((QueueDeclaredRecovery) obj);
		}

		public override int GetHashCode()
		{
			return (_queue != null ? _queue.GetHashCode() : 0);
		}
	}
}