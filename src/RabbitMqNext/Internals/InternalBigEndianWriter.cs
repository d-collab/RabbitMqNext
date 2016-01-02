namespace RabbitMqNext.Internals
{
	using System;
	using System.Runtime.CompilerServices;

	public class InternalBigEndianWriter //: BinaryWriter
	{
		private readonly Action<byte[], int, int> _writer;

		public InternalBigEndianWriter(Action<byte[],int,int> writer)
		{
			_writer = writer;
		}

		public void Write(byte[] buffer, int offset, int count)
		{
			InternalWrite(buffer, offset, count, false);
		}

		public void Write(byte b)
		{
			InternalWrite(new [] { b }, 0, 1, false);
		}

		public void Write(sbyte b)
		{
			InternalWrite(new[] { (byte) b }, 0, 1, false);
		}

		public void Write(short i)
		{
			var buffer = new []
			{
				(byte) ((i & 0xFF00) >> 8),
				(byte) (i & 0x00FF)
			};
			InternalWrite(buffer, 0, buffer.Length, false);
		}

		public void Write(ushort i)
		{
			var buffer = new[]
			{
				(byte)((i & 0xFF00) >> 8),
				(byte)(i & 0x00FF)
			};
			InternalWrite(buffer, 0, buffer.Length, false);
		}

		public void Write(int i)
		{
			var buffer = new[]
			{
				(byte)((i & 0xFF000000) >> 24),
				(byte)((i & 0x00FF0000) >> 16),
				(byte)((i & 0x0000FF00) >> 8),
				(byte)(i & 0x000000FF),
			};
			InternalWrite(buffer, 0, buffer.Length, false);
		}

		public void Write(uint i)
		{
//			var buffer = new[]
//			{
//				(byte)((i & 0xFF000000) >> 24),
//				(byte)((i & 0x00FF0000) >> 16),
//				(byte)((i & 0x0000FF00) >> 8),
//				(byte)(i & 0x000000FF),
//			};
			var buffer = BitConverter.GetBytes((uint) i);

			InternalWrite(buffer, 0, buffer.Length, true);
		}

		public void Write(long i)
		{
			var i1 = (int)(i >> 32);
			var i2 = (int)i;
			Write(i1);
			Write(i2);
		}

		public void Write(ulong i)
		{
			var i1 = (uint)(i >> 32);
			var i2 = (uint)i;
			Write(i1);
			Write(i2);
		}

		public void Write(float f)
		{
			var buffer = BitConverter.GetBytes(f);
			InternalWrite(buffer, 0, buffer.Length, reverseIfLittleEndian: true);
		}

		public void Write(double d)
		{
			var buffer = BitConverter.GetBytes(d);
			InternalWrite(buffer, 0, buffer.Length, reverseIfLittleEndian: true);
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		private void InternalWrite(byte[] buffer, int offset, int count, bool reverseIfLittleEndian)
		{
			if (reverseIfLittleEndian && BitConverter.IsLittleEndian)
			{
				Array.Reverse(buffer, offset, count);
			}
			_writer(buffer, offset, count);
		}
	}
}