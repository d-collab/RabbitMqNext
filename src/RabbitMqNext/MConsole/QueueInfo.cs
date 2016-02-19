namespace RabbitMqNext.MConsole
{
	using Newtonsoft.Json;

	public class QueueInfo
	{
		[JsonProperty("memory")]
		public long Memory { get; set; }

		[JsonProperty("messages")]
		public long Messages { get; set; }

		[JsonProperty("name")]
		public string Name { get; set; }

		[JsonProperty("vhost")]
		public string VirtualHost { get; set; }

		[JsonProperty("durable")]
		public bool Durable { get; set; }

		[JsonProperty("auto_delete")]
		public bool AutoDelete { get; set; }

		[JsonProperty("node")]
		public string Node { get; set; }
	}
}