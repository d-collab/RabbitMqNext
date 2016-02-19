namespace RabbitMqNext.MConsole
{
	using Newtonsoft.Json;

	public class UserVHostPermissionsInfo 
	{
		[JsonProperty("user")]
		private string Name { get; set; }

		[JsonProperty("vhost")]
		private string VHost { get; set; }

		[JsonProperty("configure")]
		private string Configure { get; set; }
		
		[JsonProperty("write")]
		private string Write { get; set; }
		
		[JsonProperty("read")]
		private string Read { get; set; }
	}
}