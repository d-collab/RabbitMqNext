namespace RabbitMqNext.Internals
{
	using System;
	using System.Threading.Tasks;

	internal class InternalBigEndianReader
	{
		private readonly RingBufferStream _ringBufferStream;

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

		public async Task<byte> ReadByte()
		{
			var tempBuffer = new byte[1];
			await FillBuffer(tempBuffer, tempBuffer.Length);
			return tempBuffer[0];
		}

		public async Task<sbyte> ReadSByte()
		{
			return (sbyte) await ReadByte();
		}

		public async Task<float> ReadSingle()
		{
			var tempBuffer = new byte[4];
			await FillBuffer(tempBuffer, tempBuffer.Length);
			return BitConverter.ToSingle(tempBuffer, 0);
		}

		public async Task<double> ReadDouble()
		{
			var tempBuffer = new byte[8];
			await FillBuffer(tempBuffer, tempBuffer.Length);
			return BitConverter.ToDouble(tempBuffer, 0);
		}

		public async Task<short> ReadInt16()
		{
			var tempBuffer = new byte[2];
			await FillBuffer(tempBuffer, tempBuffer.Length);
			return BitConverter.ToInt16(tempBuffer, 0);
		}

		public async Task<int> ReadInt32()
		{
			var tempBuffer = new byte[4];
			await FillBuffer(tempBuffer, tempBuffer.Length);
			return BitConverter.ToInt32(tempBuffer, 0);
		}

		public async Task<long> ReadInt64()
		{
			var tempBuffer = new byte[8];
			await FillBuffer(tempBuffer, tempBuffer.Length);
			return BitConverter.ToInt64(tempBuffer, 0);
		}

		public async Task<ushort> ReadUInt16()
		{
			var tempBuffer = new byte[2];
			await FillBuffer(tempBuffer, tempBuffer.Length);
			return BitConverter.ToUInt16(tempBuffer, 0);
		}

		public async Task<uint> ReadUInt32()
		{
			var tempBuffer = new byte[4];
			await FillBuffer(tempBuffer, tempBuffer.Length);
			return BitConverter.ToUInt32(tempBuffer, 0);
		}

		public async Task<ulong> ReadUInt64()
		{
			var tempBuffer = new byte[8];
			await FillBuffer(tempBuffer, tempBuffer.Length);
			return BitConverter.ToUInt64(tempBuffer, 0);
		}

		
	}
}