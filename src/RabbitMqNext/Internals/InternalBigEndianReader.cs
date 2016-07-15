namespace RabbitMqNext.Internals
{
	using System;
	using System.Threading.Tasks;
	using RingBuffer;

	// Consider Buffer.SetByte/GetByte for perf
	public class InternalBigEndianReader
	{
		internal readonly RingBufferStreamAdapter _ringBufferStream;

		private readonly byte[] _oneByteArray = new byte[1];
		private readonly byte[] _twoByteArray = new byte[2];
		private readonly byte[] _fourByteArray = new byte[4];
		private readonly byte[] _eightByteArray = new byte[8];


		internal InternalBigEndianReader(RingBufferStreamAdapter ringBufferStream)
		{
			_ringBufferStream = ringBufferStream;
		}

		public void FillBufferWithLock(byte[] buffer, int count, bool reverse = true)
		{
			int totalRead = 0;
			while (totalRead < count)
			{
				totalRead += _ringBufferStream.Read(buffer, totalRead, count - totalRead, fillBuffer: true);
			}
			if (reverse && BitConverter.IsLittleEndian && count > 1)
			{
				Array.Reverse(buffer);
			}
		}

		public byte ReadByte()
		{
			FillBufferWithLock(_oneByteArray, 1, false);
			return _oneByteArray[0];
		}

		public sbyte ReadSByte()
		{
			// return (sbyte) await ReadByte();
			return (sbyte) ReadByte();
		}

		public float ReadSingle()
		{
			FillBufferWithLock(_fourByteArray, 4);
			return BitConverter.ToSingle(_fourByteArray, 0);
		}

		public double ReadDouble()
		{
			FillBufferWithLock(_eightByteArray, 8);
			return BitConverter.ToDouble(_eightByteArray, 0);
		}

		public short ReadInt16()
		{
			FillBufferWithLock(_twoByteArray, 2);
			return BitConverter.ToInt16(_twoByteArray, 0);
		}

		public int ReadInt32()
		{
			FillBufferWithLock(_fourByteArray, 4);
			return BitConverter.ToInt32(_fourByteArray, 0);
		}

		public long ReadInt64()
		{
			FillBufferWithLock(_eightByteArray, 8);
			return BitConverter.ToInt64(_eightByteArray, 0);
		}

		public ushort ReadUInt16()
		{
			FillBufferWithLock(_twoByteArray, 2);
			return BitConverter.ToUInt16(_twoByteArray, 0);
		}

		public uint ReadUInt32()
		{
			FillBufferWithLock(_fourByteArray, 4);
			return BitConverter.ToUInt32(_fourByteArray, 0);
		}

		public ulong ReadUInt64()
		{
			FillBufferWithLock(_eightByteArray, 8);
			return BitConverter.ToUInt64(_eightByteArray, 0);
		}

		public Task SkipBy(int offset)
		{
			return _ringBufferStream._ringBuffer.Skip(offset);
		}

		public decimal ReadDecimal()
		{
			// byte + long (9 total)
			
			// TODO: convert from Amqp decimal to .net decimal

			return 0;
		}
	}
}