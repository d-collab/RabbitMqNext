namespace RabbitMqNext.MConsole
{
	using Newtonsoft.Json;

	public class ExchangeInfo
	{
		[JsonProperty("name")]
		public string Name { get; set; }

		[JsonProperty("vhost")]
		public string VirtualHost { get; set; }

		[JsonProperty("type")]
		public string ExchangeType { get; set; }

		[JsonProperty("durable")]
		public bool Durable { get; set; }

		[JsonProperty("auto_delete")]
		public bool AutoDelete { get; set; }

		[JsonProperty("internal")]
		public bool Internal { get; set; }
	}
}