namespace RabbitMqNext.MConsole
{
	using Newtonsoft.Json;

	public class VHostInfo
	{
		[JsonProperty("name")]
		public string Name { get; set; }
	}
}