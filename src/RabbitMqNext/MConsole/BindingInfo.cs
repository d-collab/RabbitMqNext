namespace RabbitMqNext.MConsole
{
	using Newtonsoft.Json;

	public class BindingInfo
	{
		[JsonProperty("source")]
		public string Source { get; set; }

		[JsonProperty("vhost")]
		public string VirtualHost { get; set; }

		[JsonProperty("destination")]
		public string Destination { get; set; }

		[JsonProperty("destination_type")]
		public string DestinationType { get; set; }

		[JsonProperty("routing_key")]
		public string RoutingKey { get; set; }

		[JsonProperty("properties_key")]
		public string PropertiesKey { get; set; }
	}
}