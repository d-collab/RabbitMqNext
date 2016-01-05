namespace RabbitMqNext.Internals
{
	using System;
	using System.Threading.Tasks;

	internal class InternalBigEndianReader
	{
		internal readonly RingBufferStream _ringBufferStream;

		private readonly byte[] _oneByteArray = new byte[1];
		private readonly byte[] _twoByteArray = new byte[2];
		private readonly byte[] _fourByteArray = new byte[4];
		private readonly byte[] _eightByteArray = new byte[8];

		public InternalBigEndianReader(RingBufferStream ringBufferStream)
		{
			_ringBufferStream = ringBufferStream;
		}

		public long Position { get { return _ringBufferStream.Position; } }

		public async Task FillBuffer(byte[] buffer, int count, bool reverse = true)
		{
			int totalRead = 0;
			while (totalRead < count)
			{
				var read = await _ringBufferStream.ReadTaskAsync(buffer, totalRead, count - totalRead);
				totalRead += read;
			}
			if (reverse && BitConverter.IsLittleEndian && count > 1)
			{
				Array.Reverse(buffer);
			}
		}

		public byte ReadByte()
		{
			FillBuffer(_oneByteArray, 1).Wait();
			return _oneByteArray[0];
		}

		public sbyte ReadSByte()
		{
			return (sbyte) ReadByte();
		}

		public async Task<float> ReadSingle()
		{
			await FillBuffer(_fourByteArray, 4);
			return BitConverter.ToSingle(_fourByteArray, 0);
		}

		public async Task<double> ReadDouble()
		{
			await FillBuffer(_eightByteArray, 8);
			return BitConverter.ToDouble(_eightByteArray, 0);
		}

		public short ReadInt16()
		{
			FillBuffer(_twoByteArray, 2).Wait();
			return BitConverter.ToInt16(_twoByteArray, 0);
		}

		public int ReadInt32()
		{
			FillBuffer(_fourByteArray, 4).Wait();
			return BitConverter.ToInt32(_fourByteArray, 0);
		}

		public long ReadInt64()
		{
			FillBuffer(_eightByteArray, 8).Wait();
			return BitConverter.ToInt64(_eightByteArray, 0);
		}

		public ushort ReadUInt16()
		{
			FillBuffer(_twoByteArray, 2).Wait();
			return BitConverter.ToUInt16(_twoByteArray, 0);
		}

		public uint ReadUInt32()
		{
			FillBuffer(_fourByteArray, 4).Wait();
			return BitConverter.ToUInt32(_fourByteArray, 0);
		}

		public ulong ReadUInt64()
		{
			FillBuffer(_eightByteArray, 8).Wait();
			return BitConverter.ToUInt64(_eightByteArray, 0);
		}
	}
}