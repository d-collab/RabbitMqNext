namespace RabbitMqNext
{
	using System;
	using System.Runtime.CompilerServices;
	using System.Threading;
	using System.Threading.Tasks;


	public class RpcHelper : BaseRpcHelper<MessageDelivery>
	{
		const string LogSource = "RpcHelper";

		private RpcHelper(Channel channel, int maxConcurrentCalls, ConsumeMode mode, int? timeoutInMs)
			: base(channel, maxConcurrentCalls, mode, timeoutInMs)
		{
		}

		public static async Task<RpcHelper> Create(Channel channel, int maxConcurrentCalls, ConsumeMode mode, int? timeoutInMs)
		{
			var instance = new RpcHelper(channel, maxConcurrentCalls, mode, timeoutInMs);
			await instance.Setup().ConfigureAwait(false);
			return instance;
		}

		/// <summary>
		/// Sends a requests message and awaits for a reply in the temporary and exclusive queue created, matching the correlationid
		/// that is unique to this request.
		/// </summary>
		/// <param name="exchange"></param>
		/// <param name="routing"></param>
		/// <param name="properties"></param>
		/// <param name="buffer"></param>
		/// <returns></returns>
		public Task<MessageDelivery> Call(string exchange, string routing, BasicProperties properties, ArraySegment<byte> buffer)
		{
			if (!_operational) throw new Exception("Can't make RPC call when connection in recovery");

			_semaphoreSlim.Wait();

			uint correlationId;
			long pos;
			var tcs = SecureSpotAndUniqueCorrelationId(out pos, out correlationId);
			if (tcs == null)
			{
				_semaphoreSlim.Release();

				// NOTE: If our use of semaphore is correct, this should never happen:
				LogAdapter.LogError(LogSource, "Maxed calls: " + _maxConcurrentCalls);
				throw new Exception("reached max calls");
			}
			int cookie = tcs.Task.Id;

			try
			{
				var prop = (properties == null || properties == BasicProperties.Empty) ? _channel.RentBasicProperties() : properties;

				prop.CorrelationId = BuildFullCorrelation(cookie, correlationId);
				prop.ReplyTo = _replyQueueName.Name;

				// TODO: confirm this doesnt cause more overhead to rabbitmq
				if (_timeoutInMs.HasValue)
				{
					// prop.Expiration = _timeoutInMs.ToString();
				}

				_channel.BasicPublishFast(exchange, routing, true, prop, buffer);
			}
			catch (Exception ex)
			{
				// release spot
				// Interlocked.Exchange(ref _pendingCalls[pos], null);
				ReleaseSpot(pos, cookie);

				_semaphoreSlim.Release();

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

				var item = _pendingCalls[pos];
				TaskCompletionSource<MessageDelivery> tcs;

				if (item.cookie != cookie 
					|| (tcs = Interlocked.Exchange(ref item.tcs, null)) == null)
				{
					// the helper was disposed and the task list was drained.
					// or the call timeout'ed previously
					return Task.CompletedTask;
				}

				if (_mode == ConsumeMode.SingleThreaded)
				{
					delivery = delivery.SafeClone();
				}
				else
				{
					delivery.TakenOver = true;
				}

				if (tcs.TrySetResult(delivery)) // <- this races with DrainPendingCalls && Timeoutcheck...
				{
					// ...but we want just one call to semaphore.Release
					_semaphoreSlim.Release();
				}
			}
			catch (Exception error)
			{
				LogAdapter.LogError(LogSource, "Error on OnReplyReceived", error);
			}
			finally
			{
				ReleaseSpot(pos, cookie);
			}

			return Task.CompletedTask;
		}
	}
}