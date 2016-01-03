namespace RabbitMqNext.Internals
{
	using System;
	using System.Buffers;
	using System.Collections;
	using System.Collections.Generic;
	using System.Text;
	using System.Threading.Tasks;


	internal class AmqpPrimitivesReader
	{
		private readonly InternalBigEndianReader _reader;
		private readonly ArrayPool<byte> _bufferPool = new DefaultArrayPool<byte>(1024 * 15, 20);
		private readonly byte[] _smallBuffer = new byte[300];

		public AmqpPrimitivesReader(InternalBigEndianReader reader)
		{
			_reader = reader;
		}

		public async Task<byte> ReadOctet()
		{
			return await _reader.ReadByte();
		}

		public async Task<ushort> ReadShort()
		{
			return await _reader.ReadUInt16();
		}

		public async Task<IDictionary<string, object>> ReadTable()
		{
			// unbounded allocation! bad
			IDictionary<string, object> table = new Dictionary<string, object>(capacity: 11);

			long tableLength = await _reader.ReadUInt32();
			if (tableLength == 0) return table;

			var endOfTable = _reader.Position + tableLength;

			while (_reader.Position < endOfTable)
			{
				var key = await ReadShortStr();
				var value = await ReadFieldValue();

				table[key] = value;
			}

			return table;
		}

		public async Task<string> ReadShortStr()
		{
			int byteCount = await _reader.ReadByte();
			if (byteCount == 0) return string.Empty;

			await _reader.FillBuffer(_smallBuffer, byteCount, reverse: false);
			var str = Encoding.UTF8.GetString(_smallBuffer, 0, byteCount);
			return str;
		}

		public async Task<string> ReadLongstr()
		{
			var byteCount = (int) await _reader.ReadUInt32();
			if (byteCount == 0) return string.Empty;

			var buffer = _bufferPool.Rent(byteCount);
			try
			{
				await _reader.FillBuffer(buffer, byteCount, reverse: false);
				var str = Encoding.UTF8.GetString(buffer, 0, byteCount);
				return str;
			}
			finally
			{
				_bufferPool.Return(buffer);
			}
		}

		public async Task<IList> ReadArray()
		{
			// unbounded allocation again! bad!
			IList array = new List<object>(capacity: 10);

			var arrayLength = (int) await _reader.ReadUInt32();
			if (arrayLength == 0) return array;
			
			var endPosition = _reader.Position + arrayLength;
			while (_reader.Position < endPosition)
			{
				var value = await ReadFieldValue();
				array.Add(value);
			}

			return array;
		}

		public async Task<object> ReadFieldValue()
		{
			object value = null;
			var discriminator = await _reader.ReadByte();

			switch ((char)discriminator)
			{
				case 'S':
					value = await this.ReadLongstr();
					break;
				case 'I':
					value = await _reader.ReadInt32();
					break;
				case 'A':
					value = await this.ReadArray();
					break;
				case 'b':
					value = await _reader.ReadSByte();
					break;
				case 'd':
					value = await _reader.ReadDouble();
					break;
				case 'f':
					value = await _reader.ReadSingle();
					break;
				case 'l':
					value = await _reader.ReadInt64();
					break;
				case 's':
					value = await _reader.ReadInt16();
					break;
				case 't':
					value = await _reader.ReadByte() != 0;
					break;
//				case 'x':
//					value = new BinaryTableValue(ReadLongstr(reader));
//					break;
//				case 'D':
//					// value = ReadDecimal(reader);
//					break;
//				case 'T':
//					// value = ReadTimestamp(reader);
//					break;
				case 'F':
					value = await this.ReadTable();
					break;
				case 'V':
					value = null;
					break;
				default:
					throw new Exception("Unexpected type discriminator: " + (char)discriminator);
			}
			return value;
		}

		public async Task<uint> ReadLong()
		{
			return await _reader.ReadUInt32();
		}
	}
}