namespace RabbitMqNext.Internals
{
    using System;
    using System.Buffers;
    using System.Collections;
    using System.Collections.Generic;
    using System.Text;
    using RingBuffer;
    using System.Linq;

    internal class AmqpPrimitivesReader
	{
		private readonly ArrayPool<byte> _bufferPool = ArrayPool<byte>.Create(131072, 20); // typical max frame = 131072 
		private readonly byte[] _smallBuffer = new byte[300];
		private InternalBigEndianReader _reader;

		private const bool InternStrings = false;

		public uint? FrameMaxSize { get; set; }

		public void Initialize(InternalBigEndianReader reader)
		{
			_reader = reader;
		}

		public byte ReadOctet()
		{
			return _reader.ReadByte();
		}

		public ushort ReadShort()
		{
			return _reader.ReadUInt16();
		}

		public IDictionary<string, object> ReadTable()
		{
            return ReadKeyValues().ToDictionary(kv => kv.Key, kv => kv.Value);
        }

		public IEnumerable<KeyValuePair<String, object>> ReadKeyValues()
		{
			uint tableLength = _reader.ReadUInt32();
			if (tableLength == 0) return new KeyValuePair<String, object>[] { };

            var kvs = new List<KeyValuePair<String, object>>();
			var marker = new RingBufferPositionMarker(_reader._ringBufferStream);
			while (marker.LengthRead < tableLength)
			{
				string key = ReadShortStr();
				object value = ReadFieldValue();
                kvs.Add(new KeyValuePair<string, object>(key, value));
			}
            return kvs;
		}

		public string ReadShortStr()
		{
			int byteCount = _reader.ReadByte();
			if (byteCount == 0) return string.Empty;

			_reader.FillBufferWithLock(_smallBuffer, byteCount, reverse: false);
			var str = Encoding.UTF8.GetString(_smallBuffer, 0, byteCount);

#pragma warning disable 162
			if (InternStrings)
			{
				return String.Intern(str);
			}
			return str;
#pragma warning restore 162
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
#pragma warning disable 162
				if (InternStrings)
				{
					return String.Intern(str);
				}
				return str;
#pragma warning restore 162
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

			var marker = new RingBufferPositionMarker(_reader._ringBufferStream);
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
					value = this.ReadArray();
					break;
				case 'b':
					value = (sbyte) _reader.ReadSByte();
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
				case 'D':
					value = _reader.ReadDecimal();
					break;
				case 'T':
					value = ReadTimestamp();
					break;
				case 'F':
					value = this.ReadTable();
					break;
				case 'V':
					value = null;
					break;
				default:
					throw new Exception("Unexpected type discriminator: " + (char)discriminator);
			}
			return value;
		}

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