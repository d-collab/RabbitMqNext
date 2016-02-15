namespace PerfTest
{
	using ProtoBuf;

	[ProtoContract(SkipConstructor = true)]
	public class Messages1
	{
		[ProtoMember(1)]
		public int Seq { get; set; }
//		[ProtoMember(2)]
//		public long Ticks { get; set; }
		[ProtoMember(3)]
		public string Something { get; set; }
	}
}