namespace RabbitMqNext
{
	using System;
	using System.Collections.Generic;
	using System.Runtime.CompilerServices;
	using System.Threading;
	using System.Threading.Tasks;
	using Internals;
	using Internals.RingBuffer;


	public class RpcAggregateHelper : BaseRpcHelper<IEnumerable<MessageDelivery>>
	{
		private static readonly char[] Separator = { '_' };

		internal class AggState
		{
			// padding to reduce false sharing problems - still needs perf review
			public readonly PaddingForInt32 left;
			
			// initialized to the min expected
			public int waitingCount;

			public readonly PaddingForInt32 right;
			
			// random, assigned at the start of the call. immutable thereafter
			public int cookie;

			public readonly List<MessageDelivery> received = new List<MessageDelivery>(capacity: 2);
		}

		// protected by the SecureSpotAndUniqueCorrelationId() guard
		private readonly AggState[] _pendingAggregationState;


		private RpcAggregateHelper(Channel channel, int maxConcurrentCalls, 
								   ConsumeMode mode, int? timeoutInMs)
			: base(channel, maxConcurrentCalls, mode, timeoutInMs)
		{
			_pendingAggregationState = new AggState[maxConcurrentCalls];
			for (int i = 0; i < maxConcurrentCalls; i++)
			{
				_pendingAggregationState[i] = new AggState();
			}
		}

		public static async Task<RpcAggregateHelper> Create(Channel channel, int maxConcurrentCalls, ConsumeMode mode, int? timeoutInMs)
		{
			var instance = new RpcAggregateHelper(channel, maxConcurrentCalls, mode, timeoutInMs);
			await instance.Setup().ConfigureAwait(false);
			return instance;
		}
	
		protected override Task OnReplyReceived(MessageDelivery delivery)
		{
			long pos;
			int cookie;
			uint correlationIdVal;
			GetPosAndCookieFromCorrelationId(delivery.properties.CorrelationId, 
											 out correlationIdVal, out pos, out cookie);

			var state = _pendingAggregationState[pos];
			var completed = false;
			IEnumerable<MessageDelivery> aggreDeliveries = null;
			lock (state)
			{
				if (state.cookie != cookie)
				{
					// most likely this is a late reply for something that has already timeout
					return Task.CompletedTask;
				}

				completed = Interlocked.Decrement(ref state.waitingCount) == 0;

				if (completed)
				{
					// no need to clone last one
					state.received.Add(delivery); 

					aggreDeliveries = state.received.ToArray();

					// reset cookie 
					Interlocked.Exchange(ref state.cookie, 0);
				}
				else
				{
					state.received.Add(delivery.SafeClone());
				}
			}

			if (!completed) // still waiting for more replies
			{
				return Task.CompletedTask;
			}


			var taskLight = Interlocked.Exchange(ref _pendingCalls[pos], null);

			if (taskLight == null || taskLight.Id != correlationIdVal)
			{
				// the helper was disposed and the task list was drained.
				// if (_disposed) return Task.CompletedTask;

				// other situation, the call timeout'ed previously
			}
			else
			{
				try
				{
					taskLight.Id = 0;
					taskLight.SetResult(aggreDeliveries);
				}
				finally
				{
					_semaphoreSlim.Release();
				}
			}

			return Task.CompletedTask;
		}

		/// <summary>
		/// The request message is expected to have multiple receivers, and multiple replies. 
		/// The replies will be aggregated and returned, respecting up to a minimum set by minExpectedReplies, and 
		/// if unsuccessful a timeout error will be thrown.
		/// </summary>
		public TaskSlim<IEnumerable<MessageDelivery>> CallAggregate(string exchange, string routing, BasicProperties properties,
			ArraySegment<byte> buffer, int minExpectedReplies)
		{
			_semaphoreSlim.Wait();

			var task = _taskResultPool.GetObject();

			uint correlationId;
			long pos;
			if (!SecureSpotAndUniqueCorrelationId(task, out pos, out correlationId))
			{
				_semaphoreSlim.Release();

				// NOTE: If our use of semaphore is correct, this should never happen:
				LogAdapter.LogError("RpcAggregateHelper", "Maxed calls: " + _maxConcurrentCalls);
				task.SetException(new Exception("reached max calls"));
				return task;
			}

			var cookie = Rnd.Next();
			var state = _pendingAggregationState[pos];
			lock (state) // bad, but chances of s*** happening are too high
			{
				Interlocked.Exchange(ref state.waitingCount, minExpectedReplies);
				Interlocked.Exchange(ref state.cookie, cookie);
				state.received.Clear();
			}

			task.Id = correlationId; // so we can confirm we have the right instance later
			task.Started = DateTime.Now.Ticks;

			try
			{
				var prop = properties ?? _channel.RentBasicProperties();
				prop.CorrelationId = correlationId + Separator[0].ToString() + cookie;
				prop.ReplyTo = _replyQueueName.Name;

				// TODO: confirm this doesnt cause more overhead to rabbitmq
				if (_timeoutInMs.HasValue)
				{
					prop.Expiration = _timeoutInMs.ToString();
				}

				_channel.BasicPublishFast(exchange, routing, true, prop, buffer);
			}
			catch (Exception ex)
			{
				// release spot
				Interlocked.Exchange(ref _pendingCalls[correlationId % _maxConcurrentCalls], null);

				_semaphoreSlim.Release();

				task.SetException(ex);
			}

			return task;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		private void GetPosAndCookieFromCorrelationId(string correlationId, 
			out uint correlationIdVal, out long pos, out int cookie)
		{
			var entries = correlationId.Split(Separator, StringSplitOptions.RemoveEmptyEntries);
			if (entries.Length != 2) 
				throw new Exception("Expecting a correctionId to contain pos and cookie info, but found none. Received " + correlationId);

			correlationIdVal = UInt32.Parse(entries[0]);
			cookie = Int32.Parse(entries[1]);
			pos = correlationIdVal % _maxConcurrentCalls;
		}
	}
}