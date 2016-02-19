namespace RabbitMqNext.MConsole
{
	using Newtonsoft.Json;

	public class UserVHostPermissionsInfo 
	{
		[JsonProperty("user")]
		public string Name { get; set; }

		[JsonProperty("vhost")]
		public string VHost { get; set; }

		[JsonProperty("configure")]
		public string Configure { get; set; }
		
		[JsonProperty("write")]
		public string Write { get; set; }
		
		[JsonProperty("read")]
		public string Read { get; set; }
	}
}