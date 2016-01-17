namespace RabbitMqNext.Internals
{
	using System;
	using System.Buffers;
	using System.Collections;
	using System.Collections.Generic;
	using System.Text;

	internal class AmqpPrimitivesWriter
	{
		internal const int BufferSize = 1024 * 128;

		internal readonly InternalBigEndianWriter _writer;
		private readonly ArrayPool<byte> _bufferPool;
		internal readonly ObjectPool<ReusableTempWriter> _memStreamPool;

		private readonly byte[] _smallBuffer = new byte[300];

		public uint? FrameMaxSize { get; set; }

		public AmqpPrimitivesWriter(InternalBigEndianWriter writer, ArrayPool<byte> bufferPool,
									ObjectPool<ReusableTempWriter> memStreamPool)
		{
			_writer = writer;

			_bufferPool = bufferPool ?? new DefaultArrayPool<byte>(BufferSize, 5);
			if (memStreamPool == null)
			{
				memStreamPool = new ObjectPool<ReusableTempWriter>(() => 
				{
					Console.WriteLine("Creating new writer...");
					return new ReusableTempWriter(new DefaultArrayPool<byte>(BufferSize, 5), _memStreamPool);
				}, 6);
			}
			_memStreamPool = memStreamPool;
		}

		public void WriteOctet(byte b)
		{
			_writer.Write(b);
		}

		public void WriteUShort(ushort b)
		{
			_writer.Write(b);
		}

		public void WriteULong(ulong v)
		{
			_writer.Write(v);
		}

		public void WriteLong(uint v)
		{
			_writer.Write((uint)v);
		}

		public void WriteTable(IDictionary<string, object> table)
		{
			if (table == null || table.Count == 0)
			{
				_writer.Write((uint) 0);
				return;
			}

//			WriteBufferWithPayloadFirst(w =>
//			{
//				foreach (var entry in table)
//				{
//					w.WriteShortstr(entry.Key);
//					w.WriteFieldValue(entry.Value);
//				}
//			});

			var memStream = _memStreamPool.GetObject();
			try
			{
				memStream.EnsureMaxFrameSizeSet(this.FrameMaxSize);

				foreach (var entry in table)
				{
					memStream._writer2.WriteShortstr(entry.Key);
					memStream._writer2.WriteFieldValue(entry.Value);
				}

				var payloadSize = (uint)memStream._memoryStream.Position;
				this.WriteLong(payloadSize);

				_writer.Write(memStream._memoryStream.InternalBuffer, 0, (int)payloadSize);
			}
			finally
			{
				_memStreamPool.PutObject(memStream);
			}
		}

		public void WriteArray(IList array)
		{
			if (array == null || array.Count == 0)
			{
				_writer.Write((uint) 0);
				return;
			}

			WriteBufferWithPayloadFirst(w =>
			{
				foreach (var entry in array)
				{
					w.WriteFieldValue(entry);
				}
			});
		}

		public void WriteShortstr(string str)
		{
			var len = str != null ? Encoding.UTF8.GetBytes(str, 0, str.Length, _smallBuffer, 0) : 0;
			if (len > 255) throw new Exception("Short string too long; UTF-8 encoded length=" + len + ", max=255");
				
			_writer.Write((byte)len);
			if (len > 0)
			{
				_writer.Write(_smallBuffer, 0, len);
			}
		}

		public void WriteLongstr(string str)
		{
			if (str == null)
			{
				_writer.Write((uint)0);
				return;
			}

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

		public void WriteBits(bool b1, bool b2 = false, bool b3 = false, 
							  bool b4 = false, bool b5 = false, bool b6 = false, 
							  bool b7 = false, bool b8 = false)
		{
			byte byteVal = 0;
			byteVal = b1 ? (byte)1 : (byte)0;
			byteVal |= b2 ? (byte)2 : (byte)0;
			byteVal |= b3 ? (byte)4 : (byte)0;
			byteVal |= b4 ? (byte)8 : (byte)0;
			byteVal |= b5 ? (byte)16 : (byte)0;
			byteVal |= b6 ? (byte)32 : (byte)0;
			byteVal |= b7 ? (byte)64 : (byte)0;
			byteVal |= b8 ? (byte)128 : (byte)0;
			_writer.Write(byteVal);
		}

		public void WriteTimestamp(AmqpTimestamp ts)
		{
			_writer.Write((ulong)ts.UnixTime);
		}

		public void WriteBufferWithPayloadFirst(Action<AmqpPrimitivesWriter> writeFn)
		{
			var memStream = _memStreamPool.GetObject();

			try
			{
				memStream.EnsureMaxFrameSizeSet(this.FrameMaxSize);

				writeFn(memStream._writer2);

				var payloadSize = (uint)memStream._memoryStream.Position;

				this.WriteLong(payloadSize);
				// Console.WriteLine("conclusion: payload size  " + payloadSize);

				_writer.Write(memStream._memoryStream.InternalBuffer, 0, (int)payloadSize);
			}
			finally
			{
				_memStreamPool.PutObject(memStream);
			}
		}

		public void WriteFrameWithPayloadFirst(int frameType, ushort channel, Action<AmqpPrimitivesWriter> writeFn)
		{
			var memStream = _memStreamPool.GetObject();

			try
			{
				memStream.EnsureMaxFrameSizeSet(this.FrameMaxSize);

				writeFn(memStream._writer2);

				var payloadSize = (uint)memStream._memoryStream.Position;

				this.WriteFrameStart(frameType, channel, payloadSize, null, null);

				memStream._writer2.WriteOctet(AmqpConstants.FrameEnd);

				_writer.Write(memStream._memoryStream.InternalBuffer, 0, (int)payloadSize + 1);
			}
			finally
			{
				_memStreamPool.PutObject(memStream);
			}
		}

		private readonly byte[] _frameBuff = new byte[11];
		public void WriteFrameStart(int frameType, ushort channel, uint payloadSize, ushort? classId, ushort? methodId)
		{
			_frameBuff[0] = (byte) frameType;

			_frameBuff[1] = (byte)((channel & 0xFF00) >> 8);
			_frameBuff[2] = (byte)(channel & 0x00FF);

			_frameBuff[3] = (byte)((payloadSize & 0xFF000000) >> 24);
			_frameBuff[4] = (byte)((payloadSize & 0x00FF0000) >> 16);
			_frameBuff[5] = (byte)((payloadSize & 0x0000FF00) >> 8);
			_frameBuff[6] = (byte)(payloadSize & 0x000000FF);

			if (classId.HasValue)
			{
				var cl = classId.Value;
				var me = methodId.Value;
				_frameBuff[7] = (byte)((cl & 0xFF00) >> 8);
				_frameBuff[8] = (byte)(cl & 0x00FF);
				_frameBuff[9] = (byte)((me & 0xFF00) >> 8);
				_frameBuff[10] = (byte)(me & 0x00FF);

				_writer.Write(_frameBuff, 0, 11);
			}
			else
			{
				_writer.Write(_frameBuff, 0, 7);
			}
		}

		public void WriteFrameHeader(ushort channel, ulong bodySize, BasicProperties properties)
		{
			var memStream = _memStreamPool.GetObject();

			try
			{
				memStream.EnsureMaxFrameSizeSet(this.FrameMaxSize);

				var w = memStream._writer2;
				{
//					w.WriteUShort((ushort)60);
//					w.WriteUShort((ushort)0); // weight. not used
					w.WriteULong(bodySize);

					// no support for continuation. must be less than 15 bits used
					w.WriteUShort(properties._presenceSWord);

					if (properties.IsContentTypePresent) { w.WriteShortstr(properties.ContentType); }
					if (properties.IsContentEncodingPresent) { w.WriteShortstr(properties.ContentEncoding); }
					if (properties.IsHeadersPresent) { w.WriteTable(properties.Headers); }
					if (properties.IsDeliveryModePresent) { w.WriteOctet(properties.DeliveryMode); }
					if (properties.IsPriorityPresent) { w.WriteOctet(properties.Priority); }
					if (properties.IsCorrelationIdPresent) { w.WriteShortstr(properties.CorrelationId); }
					if (properties.IsReplyToPresent) { w.WriteShortstr(properties.ReplyTo); }
					if (properties.IsExpirationPresent) { w.WriteShortstr(properties.Expiration); }
					if (properties.IsMessageIdPresent) { w.WriteShortstr(properties.MessageId); }
					if (properties.IsTimestampPresent) { w.WriteTimestamp(properties.Timestamp.Value); }
					if (properties.IsTypePresent) { w.WriteShortstr(properties.Type); }
					if (properties.IsUserIdPresent) { w.WriteShortstr(properties.UserId); }
					if (properties.IsAppIdPresent) { w.WriteShortstr(properties.AppId); }
					if (properties.IsClusterIdPresent) { w.WriteShortstr(properties.ClusterId); }
				};

				var payloadSize = (uint)memStream._memoryStream.Position;
				// payloadSize += 4; // class + weight sizes

				this.WriteFrameStart(AmqpConstants.FrameHeader, channel, payloadSize + 4, 60 /*class*/ , 0 /* weight*/);

				memStream._writer2.WriteOctet(AmqpConstants.FrameEnd);

				// write/copy the inner stream content (basicproperties + frameend)
				_writer.Write(memStream._memoryStream.InternalBuffer, 0, (int)payloadSize + 1);
			}
			finally
			{
				_memStreamPool.PutObject(memStream);
			}
		}
	}
}