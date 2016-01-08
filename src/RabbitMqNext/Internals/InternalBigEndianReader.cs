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

		private readonly Task<byte>[] _cachedByteTaskResult = new Task<byte>[256];

		internal InternalBigEndianReader(RingBufferStreamAdapter ringBufferStream)
		{
			_ringBufferStream = ringBufferStream;

			for (int i = 0; i < 256; i++)
			{
				_cachedByteTaskResult[i] = Task.FromResult((byte) i);
			}
		}

		public long Position { get { return _ringBufferStream.Position; } }

		private void FillBufferWithLock(byte[] buffer, int count, bool reverse = true)
		{
			int totalRead = 0;
			while (totalRead < count)
			{
				totalRead += _ringBufferStream.Read(buffer, totalRead, count - totalRead);
//				var t = _ringBufferStream.ReadTaskAsync(buffer, totalRead, count - totalRead);
//				if (t.IsCompleted)
//					totalRead += t.Result;
//				else
//				{
//					t.Wait();
//					totalRead += t.Result;
//				}
			}
			if (reverse && BitConverter.IsLittleEndian && count > 1)
			{
				Array.Reverse(buffer);
			}
		}

		public async Task FillBuffer(byte[] buffer, int count, bool reverse = true)
		{
			int totalRead = 0;
			while (totalRead < count)
			{
				// var read = await _ringBufferStream.ReadTaskAsync(buffer, totalRead, count - totalRead);
				var read = await _ringBufferStream.ReadAsync(buffer, totalRead, count - totalRead, _ringBufferStream.CancellationToken);
				totalRead += read;
			}
			if (reverse && BitConverter.IsLittleEndian && count > 1)
			{
				Array.Reverse(buffer);
			}
		}

		public async Task<byte> ReadByte()
		{
			await FillBuffer(_oneByteArray, 1);
			var b = _oneByteArray[0];
//			return _cachedByteTaskResult[b];
			return b;
		}

//		public Task<byte> ReadByte()
//		{
//			var t = FillBuffer(_oneByteArray, 1);
//
//			if (t.IsCompleted)
//			{
//				var b = _oneByteArray[0];
//				return _cachedByteTaskResult[b];
//			}
//			// else
//			
//			return t.ContinueWith((_1, _) =>
//			{
//				var b = _oneByteArray[0];
//				return b;
//			}, null, TaskContinuationOptions.OnlyOnRanToCompletion | TaskContinuationOptions.ExecuteSynchronously);
//		}

		public async Task<sbyte> ReadSByte()
		{
			return (sbyte) await ReadByte();
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

		public async Task<short> ReadInt16()
		{
			await FillBuffer(_twoByteArray, 2);
			return BitConverter.ToInt16(_twoByteArray, 0);
		}

		public async Task<int> ReadInt32()
		{
			await FillBuffer(_fourByteArray, 4);
			return BitConverter.ToInt32(_fourByteArray, 0);
		}

		public async Task<long> ReadInt64()
		{
			await FillBuffer(_eightByteArray, 8);
			return BitConverter.ToInt64(_eightByteArray, 0);
		}

//		public async Task<ushort> ReadUInt16()
//		{
//			await FillBuffer(_twoByteArray, 2);
//			return BitConverter.ToUInt16(_twoByteArray, 0);
//		}

		public ushort ReadUInt16()
		{
			FillBufferWithLock(_twoByteArray, 2);
			return BitConverter.ToUInt16(_twoByteArray, 0);
		}

//		public async Task<uint> ReadUInt32()
//		{
//			await FillBuffer(_fourByteArray, 4);
//			return BitConverter.ToUInt32(_fourByteArray, 0);
//		}
		
		public uint ReadUInt32()
		{
			FillBufferWithLock(_fourByteArray, 4);
			return BitConverter.ToUInt32(_fourByteArray, 0);
		}

		public async Task<ulong> ReadUInt64()
		{
			await FillBuffer(_eightByteArray, 8);
			return BitConverter.ToUInt64(_eightByteArray, 0);
		}

		public async Task SkipBy(int byteCount)
		{
			var count = 0;
			while (count < byteCount)
			{
				await FillBuffer(_oneByteArray, 1, false);
				count++;
			}
		}
	}
}