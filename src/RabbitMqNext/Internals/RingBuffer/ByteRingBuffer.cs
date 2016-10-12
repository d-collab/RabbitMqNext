namespace RabbitMqNext.Internals.RingBuffer
{
	using System;
	using System.Diagnostics;
	using System.IO;
	using System.Net.Sockets;
	using System.Runtime.CompilerServices;
	using System.Runtime.InteropServices;
	using System.Text;
	using System.Threading;
	using System.Threading.Tasks;
	using Locks;


	/// <summary>
	/// 
	/// </summary>
	/// <remarks>
	/// <![CDATA[
	/// Proof it will work even when the cursor overflows
	/// * Preconditions: Buffer < bounds of cursor, so modulus operation work
	/// * cursors are unsigned. overflows are allowed and accounted for
	/// * operations on buffer are batched 
	///   - if you want to write 20 bytes, but there's only 10 positions 
	///     til end, and 10 positions free in the beginning, then 2 batch operations will take place)
	/// 
	/// writeCur : uint = w
	/// readCur  : uint = r
	/// 
	/// normalizedWrite = writeCur % BufferSize  = w_n
	/// normalizedRead  = readCur  % BufferSize  = r_n
	/// 
	/// == Formalization of per read / write cursors == 
	///    [ Bound to range of cursor, in this case uint 0..2^32 ]
	///    
	/// -- available to read    :  w - r
	/// -- available to write   :  r - w - 1 
	/// 
	/// == Formalization of per read / write batches
	///    [ Bound to 0..BufferSize ]
	/// 
	/// -- avail to read bat  * :  w_n - r_n
	/// -- write has wrapped    :  w_n < r_n
	/// -- avail to read bat  ^ :  BufferSize - r_n (for batch)
	/// -- avail to read bat  ^ :  BufferSize - r_n + w_n  (total)
	/// 
	/// -- avail to write bat * :  BufferSize - w_n
	/// -- write will wrap (n)  :  if r_n > w_n  and (w_n + n > r_n - 1)
	/// -- avail to write bat ^ :  r_n - w_n - 1
	/// 
	///   where n = count 
	/// * must check for wrap first
	/// ^ when restricted by wrap
	/// ]]>
	/// </remarks>
	internal class ByteRingBuffer : BaseRingBuffer, IDisposable
	{
		public const int MinBufferSize = 32;
		// public const int DefaultBufferSize = 0x100000;  //  1.048.576 1mb
		// public const int DefaultBufferSize = 0x200000;  //  2.097.152 2mb
		// public const int DefaultBufferSize = 0x20000;   //    131.072
		// public const int DefaultBufferSize = 0x10000;   //     65.536
		public const int DefaultBufferSize    = 0x80000;   //    524.288

		private readonly byte[] _buffer;
		// private const int _bufferPadding = 128;
		private const int _bufferPadding = 0;

#if DEBUGSOCKET
		private readonly FileStream _fsRecvWriter;
#endif

		/// <summary>
		/// Creates with default buffer size and 
		/// default locking strategy
		/// </summary>
		public ByteRingBuffer() : this(DefaultBufferSize)
		{
		}

		/// <summary>
		/// 
		/// </summary>
		public ByteRingBuffer(int bufferSize) : base(bufferSize)
		{
			if (bufferSize < MinBufferSize) throw new ArgumentException("buffer must be at least " + MinBufferSize, "bufferSize");

			_buffer = new byte[bufferSize + (_bufferPadding * 2)];

#if DEBUGSOCKET
			var filename = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "socketout.bin");
			if (File.Exists(filename)) File.Delete(filename);
			_fsRecvWriter = new FileStream(filename, FileMode.OpenOrCreate);
#endif
		}

		// TODO: check better options: http://xoofx.com/blog/2010/10/23/high-performance-memcpy-gotchas-in-c/
		public int Write(byte[] buffer, int offset, int count, bool writeAll = true)
		{
#if DEBUG
			if (offset < 0) throw new ArgumentOutOfRangeException("offset", "must be greater or equal to 0");
			if (count <= 0) throw new ArgumentOutOfRangeException("count", "must be greater than 0");
#endif

			int totalwritten = 0;
			while (totalwritten < count)
			{
				int available;
				var writePos = this.InternalGetReadyToWriteEntries(count - totalwritten, out available);

				if (_state._resetApplied)
				{
					throw new Exception("Can't be Written since the buffer is in Reset state"); 
					// break;
				}

				if (available == 0)
				{
					if (writeAll)
					{
						_readLock.Wait(); // may be signaled during a reset
						continue;
					}
					break;
				}

				Buffer.BlockCopy(buffer, offset + totalwritten, _buffer, writePos + _bufferPadding, available);

				totalwritten += available;
				
				var newWritePos = _state._writePosition + (uint) available;
				_state._writePosition = newWritePos;      // volative write
				_state._writePositionCopy = newWritePos;  // + read

				_writeLock.Set(); // signal - if someone is waiting
			}

			return totalwritten;
		}

		public Task<int> WriteAsync(byte[] buffer, int offset, int count, bool writeAll,
			CancellationToken cancellationToken)
		{
			var written = Write(buffer, offset, count, writeAll: true);
			return Task.FromResult(written);
		}

		public void WriteBufferFromSocketRecv(Socket socket /*, bool asyncRecv = false*/)
		{
			int available = 0;
			int writePos = 0;

			while (true)
			{
				writePos = this.InternalGetReadyToWriteEntries(BufferSize, out available);

				if (_state._resetApplied)
				{
					throw new Exception("Can't be read since the buffer is in Reset state");
					// break;
				}

				if (available == 0)
					_readLock.Wait();
				else
					break;
			}

			int received = socket.Receive(_buffer, _bufferPadding + writePos, available, SocketFlags.None);

#if DEBUGSOCKET
			_fsRecvWriter.Write(_buffer, _bufferPadding + writePos, received);
			_fsRecvWriter.Flush();
#endif

			var newWritePos = _state._writePosition + (uint)received;
			_state._writePosition = newWritePos;   // volative write_temp
			_state._writePositionCopy = newWritePos;

			_writeLock.Set(); // signal - if someone is waiting
		}

		public int Read(byte[] buffer, int offset, int count, bool fillBuffer = false/*, ReadingGate fromGate = null*/)
		{
#if DEBUG
			if (offset < 0) throw new ArgumentOutOfRangeException("offset", "must be greater or equal to 0");
			if (count <= 0) throw new ArgumentOutOfRangeException("count", "must be greater than 0");
#endif

			int totalRead = 0;

			while (totalRead < count)
			{
				int available;
				int readPos = this.InternalGetReadyToReadEntries(count - totalRead, out available/*, fromGate*/);

				if (_state._resetApplied)
				{
					throw new Exception("Can't be read since the buffer is in Reset state");
					// break;
				}

				if (available == 0)
				{
					if (fillBuffer)
					{
						FillUpBufferFromSocket();
						// _writeLock.WaitOne();
						continue;
					} 
					break;
				}

				int dstOffset = offset + totalRead;

				BufferUtil.FastCopy(buffer, dstOffset, _buffer, readPos + _bufferPadding, available);


				totalRead += available;

//				if (fromGate != null)
//				{
//					lock (fromGate)
//					{
//						fromGate.gpos += (uint)available;
//						fromGate.length -= (uint)available;
//					}
//				}
//				else
				{
					// if (!fillBuffer) break;
					var newReadPos = _state._readPosition + (uint) available;
					_state._readPosition = newReadPos; // volative write
					_state._readPositionCopy = newReadPos;
				}

				_readLock.Set(); // signal - if someone is waiting
			}

			return totalRead;
		}

//		public void ReadBufferIntoSocketSend(Socket socket/*, bool asyncSend*/)
//		{
//			while(true)
//			{
//				int totalRead;
//				int readPos = this.InternalGetReadyToReadEntries(BufferSize, out totalRead/*, null*/);
//
//				if (_state._resetApplied)
//				{
//					throw new Exception("Can't be read since the buffer is in Reset state");
//					// break;
//				}
//
//				// buffer is empty.. return and expect to be called again when something gets written
//				if (totalRead == 0) 
//				{
//					_writeLock.Wait();
//					continue;
//				}
//
//				var totalSent = 0;
//				while (totalSent < totalRead)
//				{
//					var sent = socket.Send(_buffer, _bufferPadding + readPos + totalSent, totalRead - totalSent, SocketFlags.None);
//
//					totalSent += sent;
//
//					// maybe better throughput if this goes inside the inner loop
//					var newReadPos = _state._readPosition + (uint)sent;
//					_state._readPosition = newReadPos; // volative write
//					_state._readPositionCopy = newReadPos;
//
//					_readLock.Set(); // signal - if someone is waiting
//				}
//
//				break;
////				totalRead += available;
//			}
//		}

		public Task<int> Skip(int offset)
		{
			int totalSkipped = 0;

			while (totalSkipped < offset)
			{
				int available;
				int readPos = this.InternalGetReadyToReadEntries(offset - totalSkipped, out available/*, null*/);

				if (_state._resetApplied)
				{
					throw new Exception("Can't be Skipped since the buffer is in Reset state"); 
					// break;
				}

				// var available = (int)this.InternalGetReadyToReadEntries(offset - totalSkipped);
				if (available == 0)
				{
					// _writeLock.Wait();
					FillUpBufferFromSocket();
					continue;
				}

				totalSkipped += available;

				var newReadPos = _state._readPosition + (uint)available;
				_state._readPosition = newReadPos; // volative write
				_state._readPositionCopy = newReadPos;

				_readLock.Set(); // signal - if someone is waiting
			}

			return Task.FromResult(totalSkipped);
		}

//		public CancellationToken CancellationToken { get { return _cancellationToken; } }

		public void Dispose()
		{
			// _waitingStrategy.Dispose();
			_readLock.Dispose();
			_writeLock.Dispose();
		}

		private Socket _socket;

		public Action<Socket, Exception> OnNotifyClosed;

		public void SetSocket(Socket socket, Action<Socket, Exception> onSocketClosed)
		{
			_socket = socket;
//			_writeLock.Set();

			this.OnNotifyClosed = onSocketClosed;
		}

		private void FillUpBufferFromSocket()
		{
			if (_socket != null)
			{
				try
				{
					WriteBufferFromSocketRecv(_socket);
				}
				catch (Exception ex)
				{
					FireClosed(ex);

					throw;
				}
			}
			else
			{
				_writeLock.Wait();
			}
		}

		private void FireClosed(Exception exception)
		{
			var ev = this.OnNotifyClosed;
			if (ev != null)
			{
				ev(_socket, exception);
			}
		}
	}
}
