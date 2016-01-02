namespace RabbitMqNext.Internals
{
	using System;
	using System.Buffers;
	using System.Collections;
	using System.Collections.Generic;
	using System.IO;
	using System.Text;

	internal class AmqpPrimitivesWriter
	{
		private readonly InternalBigEndianWriter _writer;
		private const int BufferSize = 1024*128;
		private readonly ArrayPool<byte> _bufferPool = new DefaultArrayPool<byte>(BufferSize, 10); 

		public AmqpPrimitivesWriter(InternalBigEndianWriter writer)
		{
			_writer = writer;
		}

		public void WriteOctet(byte b)
		{
			_writer.Write(b);
		}

		public void WriteUShort(ushort b)
		{
			_writer.Write(b);
		}

		public void WriteLong(uint v)
		{
			_writer.Write((uint)v);
		}

		public void WriteWithPayloadFirst(Action<AmqpPrimitivesWriter> writeFn)
		{
			var buffer = _bufferPool.Rent(BufferSize);
			try
			{
				// BAD APPROACH. needs review. too many allocations, 
				// albeit small objects. the buffer is reused
				var memStream = new MemoryStream(buffer, 0, buffer.Length, true);
				var innerWriter = new InternalBigEndianWriter((b, off, count) =>
				{
					memStream.Write(b, off, count);
				});
				var writer2 = new AmqpPrimitivesWriter(innerWriter);

				writeFn(writer2);

				var payloadSize = (uint) memStream.Position;

				Console.WriteLine("conclusion: payload size  " + payloadSize);

				// _writer.Write((uint)payloadSize);
				this.WriteLong(payloadSize);
				_writer.Write(buffer, 0, (int)payloadSize);
			}
			finally
			{
				_bufferPool.Return(buffer);
			}
		}

		public void WriteTable(IDictionary<string, object> table)
		{
			if (table == null)
			{
				_writer.Write((uint) 0);
				return;
			}

			WriteWithPayloadFirst(w =>
			{
				foreach (KeyValuePair<string, object> entry in table)
				{
					w.WriteShortstr(entry.Key);
					w.WriteFieldValue(entry.Value);
				}
			});
		}

		public void WriteArray(IList array)
		{
			if (array == null)
			{
				_writer.Write((uint) 0);
				return;
			}

			WriteWithPayloadFirst(w =>
			{
				foreach (var entry in array)
				{
					w.WriteFieldValue(entry);
				}
			});
		}

		public void WriteShortstr(string str)
		{
			var buffer = _bufferPool.Rent(1024);
			try
			{
				var len = Encoding.UTF8.GetBytes(str, 0, str.Length, buffer, 0);
				if (len > 255) throw new Exception("Short string too long; UTF-8 encoded length=" + len + ", max=255");
				
				_writer.Write((byte)len);
				if (len > 0)
				{
					_writer.Write(buffer, 0, len);
				}
			}
			finally
			{
				_bufferPool.Return(buffer);
			}
		}

		public void WriteLongstr(string str)
		{
			var buffer = _bufferPool.Rent(1024 * 10);
			try
			{
				var len = Encoding.UTF8.GetBytes(str, 0, str.Length, buffer, 0);
				_writer.Write((uint)len);

				if (len > 0)
				{
					_writer.Write(buffer, 0, len);
				}
			}
			finally
			{
				_bufferPool.Return(buffer);
			}
		}

		public void WriteLongbyte(byte[] buffer)
		{
			_writer.Write((uint)buffer.Length);
			if (buffer.Length > 0)
			{
				_writer.Write(buffer, 0, buffer.Length);
			}
		}

		public void WriteBit(bool val)
		{
			if (val)
				_writer.Write((byte)1);
			else
				_writer.Write((byte)0);
		}

		public void WriteRaw(byte[] buffer, int offset, int count)
		{
			_writer.Write(buffer, offset, count);
		}

		private void WriteFieldValue(object value)
		{
			if (value == null)
			{
				this.WriteOctet((byte)'V');
			}
			else if (value is string)
			{
				this.WriteOctet((byte)'S');
				this.WriteLongstr(value as string);
			}
			else if (value is byte[])
			{
				this.WriteOctet((byte)'S');
				this.WriteLongbyte((byte[])value);
			}
			else if (value is int)
			{
				this.WriteOctet((byte)'I');
				_writer.Write((int)value);
			}
//			else if (value is decimal)
//			{
//				_writer.WriteOctet((byte)'D');
//				_writer.WriteDecimal((decimal)value);
//			}
//			else if (value is AmqpTimestamp)
//			{
//				_writer.WriteOctet((byte)'T');
//				_writer.WriteTimestamp((AmqpTimestamp)value);
//			}
			else if (value is IDictionary)
			{
				WriteOctet((byte)'F');
				WriteTable((IDictionary<string,object>)value);
			}
			else if (value is IList)
			{
				WriteOctet((byte)'A');
				WriteArray((IList)value);
			}
			else if (value is sbyte)
			{
				WriteOctet((byte)'b');
				_writer.Write((sbyte)value);
			}
			else if (value is double)
			{
				WriteOctet((byte)'d');
				_writer.Write((double)value);
			}
			else if (value is float)
			{
				WriteOctet((byte)'f');
				_writer.Write((float)value);
			}
			else if (value is long)
			{
				WriteOctet((byte)'l');
				_writer.Write((long)value);
			}
			else if (value is short)
			{
				WriteOctet((byte)'s');
				_writer.Write((short)value);
			}
			else if (value is bool)
			{
				WriteOctet((byte)'t');
				WriteOctet((byte)(((bool)value) ? 1 : 0));
			}
//			else if (value is BinaryTableValue)
//			{
//				WriteOctet(writer, (byte)'x');
//				WriteLongstr(writer, ((BinaryTableValue)value).Bytes);
//			}
			else
			{
				throw new Exception("Value cannot appear as table value: " + value);
			}
		}
	}
}