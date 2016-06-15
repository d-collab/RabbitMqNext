namespace RabbitMqNext
{
	using System;
	using System.IO;
	using Internals;


	public struct MessageDelivery
	{
		// public string consumerTag;
		// public string exchange;
		public ulong deliveryTag;
		public bool redelivered;
		public string routingKey;
		public int bodySize;
		public BasicProperties properties;
		public Stream stream;

		/// <summary>
		/// Very important:
		/// Should be used when the consuption is happening a single thread, but 
		/// you're holding this object for processing later. 
		/// Otherwise:
		/// The stream will move further, and the BasicProperties will be recycled, 
		/// leading to terrible nightmare-ish consequences.
		/// </summary>
		/// <returns></returns>
		public MessageDelivery SafeClone()
		{
			return new MessageDelivery
			{
				deliveryTag = deliveryTag, 
				redelivered = redelivered,
				routingKey = routingKey, 
				bodySize = bodySize, 
				properties = properties.Clone(),
				stream = CloneStream(stream, bodySize)
			};
		}

		private static Stream CloneStream(Stream originalStream, int bodySize)
		{
			if (originalStream == null) return null; 
			
			var original = originalStream as MemoryStream; // Empty Stream then
			if (original != null)
			{
				return original;
			}

			var original2 = originalStream as MemoryStream2; // Empty Stream then
			if (original2 != null)
			{
				return new MemoryStream2(original2.InnerBuffer);
			}

			return (originalStream as BaseLightStream).CloneStream(bodySize);
		}
	}
}