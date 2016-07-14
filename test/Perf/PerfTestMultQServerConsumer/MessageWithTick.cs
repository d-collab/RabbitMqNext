namespace PerfTestMultQServer
{
	using ProtoBuf;

	[ProtoContract(SkipConstructor = true)]
	public class MessageWithTick
	{
		[ProtoMember(1)]
		public string SomeRandomContent { get; set; }

		[ProtoMember(2)]
		public long SentAtInTicks { get; set; }

		[ProtoMember(3)]
		public bool IsWarmUp { get; set; }
	}
}