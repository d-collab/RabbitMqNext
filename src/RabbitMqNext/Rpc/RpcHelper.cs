namespace RabbitMqNext
{
	using System;
	using System.Collections.Generic;
	using System.Threading;
	using System.Threading.Tasks;
	using Internals;

	public class RpcHelper : BaseRpcHelper<MessageDelivery>
	{
		private RpcHelper(Channel channel, int maxConcurrentCalls, ConsumeMode mode, int? timeoutInMs)
			: base(channel, maxConcurrentCalls, mode, timeoutInMs)
		{
		}

		public static async Task<RpcHelper> Create(Channel channel, int maxConcurrentCalls, ConsumeMode mode,
			int? timeoutInMs)
		{
			var instance = new RpcHelper(channel, maxConcurrentCalls, mode, timeoutInMs);
			await instance.Setup().ConfigureAwait(false);
			return instance;
		}

		protected override Task OnReplyReceived(MessageDelivery delivery)
		{
			var correlationIndex = UInt32.Parse(delivery.properties.CorrelationId);
			var pos = correlationIndex % _maxConcurrentCalls;

			var taskLight = Interlocked.Exchange(ref _pendingCalls[pos], null);

			if (taskLight == null || taskLight.Id != correlationIndex)
			{
				// the helper was disposed and the task list was drained.
				
				// other situation, the call timeout'ed previously

				return Task.CompletedTask;
			}


			try
			{
				taskLight.Id = 0;
				taskLight.SetResult(delivery);
			}
			finally
			{
				_semaphoreSlim.Release();
			}

			return Task.CompletedTask;
		}

//		/// <summary>
//		/// The request message is expected to have multiple receivers, but the first one to reply 
//		/// wins and the result is set. The caller should add extra metadata so workers know they dont need to reply 
//		/// unless they can return a valid response (no need to reply with errors for example, unless error is semantic valid).
//		/// </summary>
//		public TaskSlim<MessageDelivery> CallMultiple(string exchange, string routing, BasicProperties properties,
//			ArraySegment<byte> buffer)
//		{
//		}

		/// <summary>
		/// Sends a requests message and awaits for a reply in the temporary and exclusive queue created, matching the correlationid
		/// that is unique to this request.
		/// </summary>
		/// <param name="exchange"></param>
		/// <param name="routing"></param>
		/// <param name="properties"></param>
		/// <param name="buffer"></param>
		/// <returns></returns>
		public TaskSlim<MessageDelivery> Call(string exchange, string routing, BasicProperties properties, ArraySegment<byte> buffer)
		{
			_semaphoreSlim.Wait();

			var task = _taskResultPool.GetObject();

			uint correlationId;
			long pos;
			if (!SecureSpotAndUniqueCorrelationId(task, out pos, out correlationId))
			{
				_semaphoreSlim.Release();

				// NOTE: If our use of semaphore is correct, this should never happen:
				LogAdapter.LogError("RpcHelper", "Maxed calls: " + _maxConcurrentCalls);
				task.SetException(new Exception("reached max calls"));
				return task;
			}

			task.Id = correlationId; // so we can confirm we have the right instance later
			task.Started = DateTime.Now.Ticks;

			try
			{
				var prop = properties ?? _channel.RentBasicProperties();
				prop.CorrelationId = correlationId.ToString();
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
		
		/*
		private TaskSlim<MessageDelivery> ReleaseSpot(string correlationId, out uint correlationIndex)
		{
			correlationIndex = UInt32.Parse(correlationId);
			var pos = correlationIndex % _maxConcurrentCalls;

			return Interlocked.Exchange(ref _pendingCalls[pos], null);
		}
		*/
	}
}