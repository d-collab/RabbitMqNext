namespace RabbitMqNext.Internals
{
	using System;
	using System.Buffers;
	using System.Collections;
	using System.Collections.Generic;
	using System.Text;
	using System.Threading.Tasks;
	using RingBuffer;


	internal class AmqpPrimitivesReader
	{
		private readonly InternalBigEndianReader _reader;
		private readonly ArrayPool<byte> _bufferPool = new DefaultArrayPool<byte>(1024 * 15, 20);
		private readonly byte[] _smallBuffer = new byte[300];

		public uint? FrameMaxSize { get; set; }

		public AmqpPrimitivesReader(InternalBigEndianReader reader)
		{
			_reader = reader;
		}

		public byte ReadOctet()
		{
			return _reader.ReadByte();
		}

//		public Task<byte> ReadOctet()
//		{
//			return _reader.ReadByte();
//		}

//		public Task<ushort> ReadShort()
//		{
//			return _reader.ReadUInt16();
//		}
		
		public ushort ReadShort()
		{
			return _reader.ReadUInt16();
		}

		public IDictionary<string, object> ReadTable()
		{
			// unbounded allocation! bad
			var table = new Dictionary<string, object>(capacity: 11);

			uint tableLength = _reader.ReadUInt32();
			if (tableLength == 0) return table;

			var marker = new RingBufferPositionMarker(_reader._ringBufferStream._ringBuffer);
			while (marker.LengthRead < tableLength)
			{
				string key = ReadShortStr();
				object value = ReadFieldValue();
				table[key] = value;
			}

			return table;
		}

		public string ReadShortStr()
		{
			int byteCount = _reader.ReadByte();
			if (byteCount == 0) return string.Empty;

			_reader.FillBufferWithLock(_smallBuffer, byteCount, reverse: false);
			var str = Encoding.UTF8.GetString(_smallBuffer, 0, byteCount);
			return str;
		}

		public string ReadLongstr()
		{
			int byteCount = (int) _reader.ReadUInt32();
			if (byteCount == 0) return string.Empty;

			var buffer = _bufferPool.Rent(byteCount);
			try
			{
				_reader.FillBufferWithLock(buffer, byteCount, reverse: false);
				var str = Encoding.UTF8.GetString(buffer, 0, byteCount);
				return str;
			}
			finally
			{
				_bufferPool.Return(buffer);
			}
		}

		public IList ReadArray()
		{
			// unbounded allocation again! bad!
			IList array = new List<object>(capacity: 10);

			var arrayLength = (int) _reader.ReadUInt32();
			if (arrayLength == 0) return array;

			var marker = new RingBufferPositionMarker(_reader._ringBufferStream._ringBuffer);
			while (marker.LengthRead < arrayLength)
			{
				object value = ReadFieldValue();
				array.Add(value);
			}

			return array;
		}

		public object ReadFieldValue()
		{
			object value = null;
			byte discriminator = _reader.ReadByte();

			switch ((char)discriminator)
			{
				case 'S':
					value = (string) this.ReadLongstr();
					break;
				case 'I':
					value = (Int32) _reader.ReadInt32();
					break;
				case 'A':
					value = (IList) this.ReadArray();
					break;
				case 'b':
					value = (sbyte)_reader.ReadSByte();
					break;
				case 'd':
					value = (double) _reader.ReadDouble();
					break;
				case 'f':
					value = (float) _reader.ReadSingle();
					break;
				case 'l':
					value = (long) _reader.ReadInt64();
					break;
				case 's':
					value = (short) _reader.ReadInt16();
					break;
				case 't':
					value = _reader.ReadByte() != 0;
					break;
//				case 'x':
//					value = new BinaryTableValue(ReadLongstr(reader));
//					break;
//				case 'D':
//					// value = ReadDecimal(reader);
//					break;
				case 'T':
					value = (AmqpTimestamp) ReadTimestamp();
					break;
				case 'F':
					value = (IDictionary<string,object>) this.ReadTable();
					break;
				case 'V':
					value = null;
					break;
				default:
					throw new Exception("Unexpected type discriminator: " + (char)discriminator);
			}
			return value;
		}

//		public Task<uint> ReadLong()
//		{
//			return _reader.ReadUInt32();
//		}

		public uint ReadLong()
		{
			return _reader.ReadUInt32();
		}

		public ulong ReadULong()
		{
			return _reader.ReadUInt64();
		}

		public byte ReadBits()
		{
			return _reader.ReadByte();
		}

		public AmqpTimestamp ReadTimestamp()
		{
			long l = _reader.ReadInt64();
			return new AmqpTimestamp(l);
		}
	}
}