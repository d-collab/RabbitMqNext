namespace RabbitMqNext.MConsole
{
	using Newtonsoft.Json;

	public class UserInfo
	{
		[JsonProperty("name")]
		private string Name { get; set; }

		[JsonProperty("tags")]
		private string Tags { get; set; }
	}
}