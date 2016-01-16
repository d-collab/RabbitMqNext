namespace RabbitMqNext.Tests
{
	using System.Threading.Tasks;
	using FluentAssertions;
	using Internals;
	using NUnit.Framework;


	[TestFixture]
	public class WriterReaderBigEndianConversions
	{
		private RingBufferStream _sharedBuffer;
		private InternalBigEndianWriter _writer;
		private InternalBigEndianReader _reader;

		[SetUp]
		public void Start()
		{
			_sharedBuffer = new RingBufferStream();

			_writer = new InternalBigEndianWriter((b,off,c) =>
			{
				_sharedBuffer.Insert(b, off, c);
			});

			_reader = new InternalBigEndianReader(_sharedBuffer);
		}

		[Test]
		public async Task Negs()
		{
			_writer.Write((int)-100);
			_writer.Write((long)-200);
			_writer.Write((float)-2000001.222444);
			_writer.Write((double)-2000001.222444);

			(await _reader.ReadInt32()).Should().Be(-100);
			(await _reader.ReadInt64()).Should().Be(-200);
			(await _reader.ReadSingle()).Should().Be(-2000001.222444f);
			(await _reader.ReadDouble()).Should().Be(-2000001.222444);
		}

		[Test]
		public async Task Bytes()
		{
			for (int i = 0; i < 1024; i++)
			{
				_writer.Write((byte) (i % 256) );
			}

			for (int i = 0; i < 1024; i++)
			{
				var b1 = await _reader.ReadByte();
				b1.Should().Be( (byte) (i % 256) );
			}
		}

		[Test]
		public async Task Int32s()
		{
			for (int i = 0; i < RingBufferStream.BufferSize / 4; i++)
			{
				_writer.Write((int)i);
			}

			for (int i = 0; i < RingBufferStream.BufferSize / 4; i++)
			{
				var int1 = await _reader.ReadInt32();
				int1.Should().Be((int) i);
			}
		}

		[Test]
		public async Task UInt32s()
		{
			for (uint i = 0; i < RingBufferStream.BufferSize / 4; i++)
			{
				_writer.Write((uint)i);
			}

			for (uint i = 0; i < RingBufferStream.BufferSize / 4; i++)
			{
				var int1 = await _reader.ReadUInt32();
				int1.Should().Be((uint)i);
			}
		}

		[Test]
		public async Task Int64s()
		{
			for (long i = 0; i < RingBufferStream.BufferSize / 8; i++)
			{
				_writer.Write((long)i);
			}

			for (long i = 0; i < RingBufferStream.BufferSize / 8; i++)
			{
				var int1 = await _reader.ReadInt64();
				int1.Should().Be((long)i);
			}
		}

		[Test]
		public async Task UInt64s()
		{
			for (ulong i = 0; i < RingBufferStream.BufferSize / 8; i++)
			{
				_writer.Write((ulong)i);
			}

			for (ulong i = 0; i < RingBufferStream.BufferSize / 8; i++)
			{
				var int1 = await _reader.ReadUInt64();
				int1.Should().Be((ulong)i);
			}
		}

		[Test]
		public async Task Singles()
		{
			for (int i = 0; i < RingBufferStream.BufferSize / 4; i++)
			{
				_writer.Write(((float) i) + 0.2844f);
			}

			for (int i = 0; i < RingBufferStream.BufferSize / 4; i++)
			{
				var f = await _reader.ReadSingle();
				f.Should().Be(((float)i) + 0.2844f);
			}
		}

		[Test]
		public async Task Doubles()
		{
			for (int i = 0; i < RingBufferStream.BufferSize / 8; i++)
			{
				_writer.Write(((double)i) + 0.2844f);
			}

			for (int i = 0; i < RingBufferStream.BufferSize / 8; i++)
			{
				var f = await _reader.ReadDouble();
				f.Should().Be(((double)i) + 0.2844f);
			}
		}
	}
}