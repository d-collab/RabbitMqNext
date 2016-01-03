namespace RabbitMqNext.Internals
{
	using System;
	using System.IO;
	using System.Runtime.CompilerServices;

	public class InternalBigEndianWriter //: BinaryWriter
	{
		private readonly Stream _outStream;
		private readonly Action<byte[], int, int> _writer;

		private byte[] _oneByteArray = new byte[1];
		private byte[] _twoByteArray = new byte[2];
		private byte[] _fourByteArray = new byte[4];
		private byte[] _eightByteArray = new byte[8];

		public InternalBigEndianWriter(Action<byte[],int,int> writer)
		{
			_writer = writer;
			_outStream = null;
		}

		public InternalBigEndianWriter(Stream outStream)
		{
			_outStream = outStream;
			_writer = null;
		}

		public void Write(byte[] buffer, int offset, int count)
		{
			InternalWrite(buffer, offset, count, false);
		}

		public void Write(byte b)
		{
			_oneByteArray[0] = b; // safe coz this is not used concurrently
			InternalWrite(_oneByteArray, 0, 1, false);
		}

		public void Write(sbyte b)
		{
			_oneByteArray[0] = (byte)b; // safe coz this is not used concurrently
			InternalWrite(_oneByteArray, 0, 1, false);
		}

		public void Write(short i)
		{
			_twoByteArray[0] = (byte)((i & 0xFF00) >> 8);
			_twoByteArray[1] = (byte)(i & 0x00FF);

//			var buffer = new []
//			{
//				(byte) ((i & 0xFF00) >> 8),
//				(byte) (i & 0x00FF)
//			};
			InternalWrite(_twoByteArray, 0, 2, false);
		}

		public void Write(ushort i)
		{
			_twoByteArray[0] = (byte)((i & 0xFF00) >> 8);
			_twoByteArray[1] = (byte)(i & 0x00FF);

//			var buffer = new[]
//			{
//				(byte)((i & 0xFF00) >> 8),
//				(byte)(i & 0x00FF)
//			};
			InternalWrite(_twoByteArray, 0, 2, false);
		}

		public void Write(int i)
		{
			_fourByteArray[0] = (byte) ((i & 0xFF000000) >> 24);
			_fourByteArray[1] = (byte) ((i & 0x00FF0000) >> 16);
			_fourByteArray[2] = (byte) ((i & 0x0000FF00) >> 8);
			_fourByteArray[3] = (byte) (i & 0x000000FF);

//			var buffer = new[]
//			{
//				(byte)((i & 0xFF000000) >> 24),
//				(byte)((i & 0x00FF0000) >> 16),
//				(byte)((i & 0x0000FF00) >> 8),
//				(byte)(i & 0x000000FF),
//			};
			InternalWrite(_fourByteArray, 0, 4, false);
		}

		public void Write(uint i)
		{
			_fourByteArray[0] = (byte)((i & 0xFF000000) >> 24);
			_fourByteArray[1] = (byte)((i & 0x00FF0000) >> 16);
			_fourByteArray[2] = (byte)((i & 0x0000FF00) >> 8);
			_fourByteArray[3] = (byte) (i & 0x000000FF);

			InternalWrite(_fourByteArray, 0, _fourByteArray.Length, false);
		}

		public void Write(long i)
		{
			_eightByteArray[0] = (byte)((i & 0x0F00000000000000) >> 512);
			_eightByteArray[1] = (byte)((i & 0x00FF000000000000) >> 128);
			_eightByteArray[2] = (byte)((i & 0x0000FF0000000000) >> 64);
			_eightByteArray[3] = (byte)((i & 0x000000FF00000000) >> 32);
			_eightByteArray[4] = (byte)((i & 0x00000000FF000000) >> 24);
			_eightByteArray[5] = (byte)((i & 0x0000000000FF0000) >> 16);
			_eightByteArray[6] = (byte)((i & 0x000000000000FF00) >> 8);
			_eightByteArray[7] = (byte) (i & 0x00000000000000FF);

//			var i1 = (int)(i >> 32);
//			var i2 = (int)i;
//			Write(i1);
//			Write(i2);
			InternalWrite(_eightByteArray, 0, _eightByteArray.Length, false);
		}

		public void Write(ulong i)
		{
			_eightByteArray[0] = (byte)((i & 0xFF00000000000000) >> 512);
			_eightByteArray[1] = (byte)((i & 0x00FF000000000000) >> 128);
			_eightByteArray[2] = (byte)((i & 0x0000FF0000000000) >> 64);
			_eightByteArray[3] = (byte)((i & 0x000000FF00000000) >> 32);
			_eightByteArray[4] = (byte)((i & 0x00000000FF000000) >> 24);
			_eightByteArray[5] = (byte)((i & 0x0000000000FF0000) >> 16);
			_eightByteArray[6] = (byte)((i & 0x000000000000FF00) >> 8);
			_eightByteArray[7] = (byte)(i & 0x00000000000000FF);

//			var i1 = (uint)(i >> 32);
//			var i2 = (uint)i;
//			Write(i1);
//			Write(i2);

			InternalWrite(_eightByteArray, 0, _eightByteArray.Length, false);
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

			if (_outStream != null)
				_outStream.Write(buffer, offset, count);

			if (_writer != null)
				_writer(buffer, offset, count);
		}
	}
}