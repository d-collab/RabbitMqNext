namespace RabbitMqNext
{
	using System;
	using System.Collections.Generic;
	using System.Runtime.CompilerServices;
	using System.Threading;
	using System.Threading.Tasks;
	using Internals.RingBuffer;


	public class RpcAggregateHelper : BaseRpcHelper<IEnumerable<MessageDelivery>>
	{
		const string LogSource = "RpcAggregateHelper";

		internal class AggState
		{
			// padding to reduce false sharing problems - still needs perf review
			// public readonly PaddingForInt32 left;
			
			// initialized to the min expected
			public int waitingCount;

			// public readonly PaddingForInt32 right;
			
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

		public static async Task<RpcAggregateHelper> Create(Channel channel, int maxConcurrentCalls, ConsumeMode mode, 
															bool captureContext = false, int? timeoutInMs = null)
		{
			var instance = new RpcAggregateHelper(channel, maxConcurrentCalls, mode, timeoutInMs)
			{
				CaptureContext = captureContext
			};
			await instance.Setup().ConfigureAwait(captureContext);
			return instance;
		}
	
		/// <summary>
		/// The request message is expected to have multiple receivers, and multiple replies. 
		/// The replies will be aggregated and returned, respecting up to a minimum set by minExpectedReplies, and 
		/// if unsuccessful a timeout error will be thrown.
		/// </summary>
		public Task<IEnumerable<MessageDelivery>> CallAggregate(string exchange, string routing, 
			BasicProperties properties,
			ArraySegment<byte> buffer, int minExpectedReplies, bool runContinuationsAsynchronously = true)
		{
			if (!_operational) throw new Exception("Can't make RPC call when connection in recovery");

			_semaphoreSlim.Wait();

			uint correlationId;
			long pos;
			var tcs = SecureSpotAndUniqueCorrelationId(runContinuationsAsynchronously, out pos, out correlationId);
			if (tcs == null)
			{
				_semaphoreSlim.Release();

				// NOTE: If our use of semaphore is correct, this should never happen:
				LogAdapter.LogError(LogSource, "Maxed calls: " + _maxConcurrentCalls);
				throw new Exception("reached max calls");
			}

			var cookie = tcs.Task.Id;
			var state = _pendingAggregationState[pos];
			lock (state) // bad, but chances of s*** happening are too high
			{
				Interlocked.Exchange(ref state.waitingCount, minExpectedReplies);
				Interlocked.Exchange(ref state.cookie, cookie);
				state.received.Clear();
			}

			try
			{
				var prop = (properties == null || properties == BasicProperties.Empty) ? _channel.RentBasicProperties() : properties;
				
				prop.CorrelationId = BuildFullCorrelation(cookie, correlationId); // correlationId + SeparatorStr + cookie;
				prop.ReplyTo = _replyQueueName.Name;

				// TODO: confirm this doesnt cause more overhead to rabbitmq
				if (_timeoutInMs.HasValue)
				{
//					prop.Expiration = _timeoutInMs.ToString();
				}

				_channel.BasicPublishFast(exchange, routing, true, prop, buffer);
			}
			catch (Exception ex)
			{
				if (ReleaseSpot(pos, cookie))
				{
					_semaphoreSlim.Release();
				}

				tcs.TrySetException(ex);
			}

			return tcs.Task;
		}

		protected override Task OnReplyReceived(MessageDelivery delivery)
		{
			long pos = 0;
			int cookie = 0;

			try
			{
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

					if (_mode == ConsumeMode.SingleThreaded)
					{
						state.received.Add(delivery.SafeClone());
					}
					else
					{
						delivery.TakenOver = true;
						state.received.Add(delivery);
					}

					if (completed)
					{
						aggreDeliveries = state.received.ToArray(); // needs extra copy

						// reset cookie 
						Interlocked.Exchange(ref state.cookie, 0);
					}
				}

				if (!completed) // still waiting for more replies
				{
					return Task.CompletedTask;
				}

				// Completed!

				var item = _pendingCalls[pos];
				TaskCompletionSource<IEnumerable<MessageDelivery>> tcs;

				if (item.cookie != cookie
					|| (tcs = Interlocked.Exchange(ref item.tcs, null)) == null)
				{
					// the helper was disposed and the task list was drained.
					// or the call timeout'ed previously
					return Task.CompletedTask;
				}

				try
				{
					if (tcs.TrySetResult(aggreDeliveries))
					{
						_semaphoreSlim.Release();
					}
				}
				finally
				{
					ReleaseSpot(pos, cookie);
				}
			}
			catch (Exception error)
			{
				LogAdapter.LogError(LogSource, "Error on OnReplyReceived", error);
				ReleaseSpot(pos, cookie);
			}

			return Task.CompletedTask;
		}
	}
}