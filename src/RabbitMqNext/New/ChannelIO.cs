namespace RabbitMqNext
{
	using System;
	using System.Threading.Tasks;
	using Internals;

	public class ChannelIO : BaseAmqpIO 
	{
		private Channel _channel;
		private Connection2 _connection;
		private ushort _channelNum;

		public ChannelIO(Channel parent)
		{
			_channel = parent;
			_connection = parent._connection;
			_channelNum = parent._channelNum;
		}

		internal override Task HandleFrame(int classMethodId)
		{
			switch (classMethodId)
			{
				case AmqpClassMethodChannelLevelConstants.ChannelClose:
					_connection._io._frameReader.Read_Channel_Close2(base.HandleCloseMethodFromServer);
					break;
//				case AmqpClassMethodChannelLevelConstants.BasicDeliver:
//					await _connection._frameReader.Read_BasicDelivery(DispatchDeliveredMessage);
//					break;
//				case AmqpClassMethodChannelLevelConstants.BasicReturn:
//					await _connection._frameReader.Read_BasicReturn(DispatchBasicReturn);
//					break;
//				// Basic Ack and NAck will be sent by the server if we enabled confirmation for this channel
//				case AmqpClassMethodChannelLevelConstants.BasicAck:
//					_connection._frameReader.Read_BasicAck(ProcessAcks);
//					break;
//				case AmqpClassMethodChannelLevelConstants.BasicNAck:
//					_connection._frameReader.Read_BasicNAck(ProcessNAcks);
//					break;
//				case AmqpClassMethodChannelLevelConstants.ChannelFlow:
//					_connection._frameReader.Read_ChannelFlow(HandleChannelFlow);
//					break;
				default:
					return base.HandReplyToAwaitingQueue(classMethodId);
			}

			return Task.CompletedTask;
		}

		internal override void InitiateCleanClose(bool initiatedByServer, ushort offendingClassId, ushort offendingMethodId)
		{
		}

		internal override void InternalDispose()
		{
		}

		private Task __SendChannelClose(ushort replyCode, string message)
		{
			var tcs = new TaskCompletionSource<bool>();

			_connection._io.SendCommand(_channelNum, 20, 40, AmqpChannelLevelFrameWriter.ChannelClose,
				reply: (channel, classMethodId, error) =>
				{
					if (classMethodId == AmqpClassMethodChannelLevelConstants.ChannelCloseOk)
					{
						// _connection._frameReader.Read_Channel_CloseOk(() =>
						{
							tcs.SetResult(true);
						}//);
					}
					else
					{
						BaseAmqpIO.SetException(tcs, error, classMethodId);
					}
					return Task.CompletedTask;
				},
				expectsReply: true,
				optArg: new FrameParameters.CloseParams()
				{
					replyCode = replyCode,
					replyText = message
				});

			return tcs.Task;
		}

		private Task __SendChannelCloseOk()
		{
			var tcs = new TaskCompletionSource<bool>();

			_connection._io.SendCommand(_channelNum, 20, 41, AmqpChannelLevelFrameWriter.ChannelCloseOk, reply: null, expectsReply: false, tcs: tcs);

			return tcs.Task;
		}

		private Task __EnableConfirmation(int maxunconfirmedMessages)
		{
			var tcs = new TaskCompletionSource<bool>();

			_connection._io.SendCommand(_channelNum, 85, 10, AmqpChannelLevelFrameWriter.ConfirmSelect(noWait: false),
				reply: (channel, classMethodId, error) =>
				{
					if (classMethodId == AmqpClassMethodChannelLevelConstants.ConfirmSelectOk)
					{
						// _connection._frameReader.Read_Channel_CloseOk(() =>
						{
							tcs.SetResult(true);
						}//);
					}
					else
					{
						BaseAmqpIO.SetException(tcs, error, classMethodId);
					}
					return Task.CompletedTask;
				},
				expectsReply: true,
				tcs: tcs);

			return tcs.Task;
		}
	}
}