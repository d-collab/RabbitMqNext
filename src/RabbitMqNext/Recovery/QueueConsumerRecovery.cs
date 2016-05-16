namespace RabbitMqNext.Recovery
{
	using System;
	using System.Collections.Generic;
	using System.Threading.Tasks;


	internal class QueueConsumerRecovery
	{
		private readonly ConsumeMode _mode;
		private readonly string _queue;
		private readonly string _consumerTag;
		private readonly bool _withoutAcks;
		private readonly bool _exclusive;
		private readonly IDictionary<string, object> _arguments;
		
		private IQueueConsumer _consumer2;
		private Func<MessageDelivery, Task> _consumer;

		public QueueConsumerRecovery(ConsumeMode mode, Func<MessageDelivery, Task> consumer, 
									 string queue, string consumerTag2, bool withoutAcks, bool exclusive, 
									 IDictionary<string, object> arguments)
			: this(mode, queue, consumerTag2, withoutAcks, exclusive, arguments)
		{
			_consumer = consumer;
		}

		public QueueConsumerRecovery(ConsumeMode mode, IQueueConsumer consumer, 
									 string queue, string consumerTag2, bool withoutAcks, bool exclusive, 
									 IDictionary<string, object> arguments)
			: this(mode, queue, consumerTag2, withoutAcks, exclusive, arguments)
		{
			_consumer2 = consumer;
		}

		private QueueConsumerRecovery(ConsumeMode mode, 
									  string queue, string consumerTag2, bool withoutAcks, bool exclusive, 
									  IDictionary<string, object> arguments)
		{
			_mode = mode;
			_queue = queue;
			_consumerTag = consumerTag2;
			_withoutAcks = withoutAcks;
			_exclusive = exclusive;
			_arguments = arguments;
		}

		public QueueConsumerRecovery(string consumerTag)
		{
			_consumerTag = consumerTag;
		}

		public async Task Apply(Channel channel)
		{
			if (LogAdapter.ExtendedLogEnabled)
				LogAdapter.LogDebug("Recovery", "Recovering consumer " + _consumerTag + " for queue " + _queue);

			if (_consumer2 != null)
			{
				await channel.BasicConsume(_mode, _consumer2, _queue, _consumerTag, _withoutAcks, _exclusive, _arguments, waitConfirmation: true).ConfigureAwait(false);
				
				_consumer2.Recovered();

				return;
			}

			await channel.BasicConsume(_mode, _consumer, _queue, _consumerTag, _withoutAcks, _exclusive, _arguments, waitConfirmation: true).ConfigureAwait(false);
		}

		public void SignalBlocked()
		{
			if (_consumer2 != null)
			{
				_consumer2.Broken();
			}
		}

		protected bool Equals(QueueConsumerRecovery other)
		{
			return StringComparer.Ordinal.Equals(_consumerTag, other._consumerTag);
		}

		public override bool Equals(object obj)
		{
			if (ReferenceEquals(null, obj)) return false;
			if (ReferenceEquals(this, obj)) return true;
			if (obj.GetType() != this.GetType()) return false;
			return Equals((QueueConsumerRecovery) obj);
		}

		public override int GetHashCode()
		{
			return (_consumerTag != null ? _consumerTag.GetHashCode() : 0);
		}
	}
}