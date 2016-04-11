namespace RabbitMqNext.Recovery
{
	using System;
	using System.Collections.Generic;

	internal class ExchangeDeclaredRecovery
	{
		private readonly string _type;
		private readonly bool _durable;
		private readonly bool _autoDelete;
		private readonly string _exchange;

		public ExchangeDeclaredRecovery(string exchange, string type, bool durable, bool autoDelete, IDictionary<string, object> arguments)
			: this(exchange, arguments)
		{
			_type = type;
			_durable = durable;
			_autoDelete = autoDelete;
		}

		public ExchangeDeclaredRecovery(string exchange, IDictionary<string, object> arguments)
		{
			_exchange = exchange;
		}

		protected bool Equals(ExchangeDeclaredRecovery other)
		{
			return StringComparer.Ordinal.Equals(_exchange, other._exchange);
		}

		public override bool Equals(object obj)
		{
			if (ReferenceEquals(null, obj)) return false;
			if (ReferenceEquals(this, obj)) return true;
			if (obj.GetType() != this.GetType()) return false;
			return Equals((ExchangeDeclaredRecovery) obj);
		}

		public override int GetHashCode()
		{
			return (_exchange != null ? _exchange.GetHashCode() : 0);
		}
	}
}