# RabbitMqNext

[![Build status](https://ci.appveyor.com/api/projects/status/l84im7iemf8w354m/branch/master?svg=true)](https://ci.appveyor.com/project/hammett/rabbitmqnext/branch/master)


Experimenting with [TPL](https://msdn.microsoft.com/en-us/library/dd460717%28v=vs.110%29.aspx) 
and buffer pools to see if a better rabbitmq client comes out of it. 

The goal is to drastically reduce contention, memory allocation and therefore GC pauses. 

The way this is accomplished is two fold:

* Api invocations return future objects (in .net the idiomatic/de-facto way is to return a Task 
  which combined with async/await makes for a decent experience) so nothing is blocked on IO.

* writes and reads related to the socket are consumer/producer of ringbuffers thus one upfront 
  big allocation and that's it. The read/write loop happen in two dedicated threads.

~~**Not ready for production use.**~~
~~* master branch is stable, but still being tested/stressed heavily~~
It's in production for a while now. 


### Current stage: 

- Handshake [Done]
- Create channel [Done]
- Exchange declare [Done]
- Queue declare [Done]
- Queue bind [Done]
- Basic publish [Done]
- Basic Ack [Done]
- Basic NAck [Done]
- connection close (started by server or client) [Done]
- Channel close (started by server or client) [Done]
- Queue Consume / Basic Deliver [Done]
- BasicReturn [Done]
- Publish confirm [Done] - _will wait the Task on BasicPublish until Ack from server, so it's slow and should be judiciously used_
- Exchange bind [Done]
- Exchange unbind [Done]
- Queue unbind [Done]
- Queue delete  [Done]
- Queue purge [Done]
- Exchange delete [Done]
- RabbitConsole from [Castle.RabbitMq](https://github.com/castleproject/Castle.RabbitMq). Very useful to set up integration tests
- ChannelFlow [Done]
- ConnectionBlock [Done] 
- Heartbeats

- Connection/channels recovery / Programming model friendly [Done]
  ** Upon disconnection, try to reconnect and when successfull restore the channels and consumers

Not planning to support:
- Any authentication method besides plain
- ~~multi body send (limit to framemax)~~ [Done]
- ~~multi body receive (limit to framemax)~~ [Done]

Special support:
- RpcHelper: for common cases of 1-to-1 rpc calls
- RpcAggregateHelper: for more advanced scenarios of sending a single message to multiple workers and aggregating the replies (think sharding)


### Sample code

##### Opening a connection
```C#
var conn = await ConnectionFactory.Connect(...);
var channel = await conn.CreateChannel();
await channel.BasicQos(0, 250, false); // setting prefetch to 250

```

##### Declares
```C#

// Task ExchangeDeclare(string exchange, string type, bool durable, bool autoDelete, IDictionary<string, object> arguments, bool waitConfirmation)
await channel1.ExchangeDeclare("test_direct_conf", "direct", true, false, null, waitConfirmation: true);


// QueueDeclare(string queue, bool passive, bool durable, bool exclusive, bool autoDelete, IDictionary<string, object> arguments, bool waitConfirmation)
var queueInfo = await channel.QueueDeclare("queue1", false, true, false, false, null, waitConfirmation: true);

// Task QueueBind(string queue, string exchange, string routingKey, IDictionary<string, object> arguments, bool waitConfirmation)
await channel1.QueueBind("queue_direct_conf", "test_direct_conf", "routing", null, waitConfirmation: true);

```

##### Publishing

Use the BasicPublishFast overload if you dont want to 'await' on the result. 
Note: the server does not confirm the receipt of a message unless your channel is on confirmation mode, 
so the task is set as complete once we write the frame to the buffer (which is not the underlying socket). 

```C#

TaskSlim BasicPublish(string exchange, string routingKey, bool mandatory, bool immediate,
			BasicProperties properties, ArraySegment<byte> buffer)

```

##### Consuming

Can be done using a delegate or a QueueConsumer subclass. There are two modes:

* ConsumeMode.SingleThreaded: uses the same frame_reader thread to invoke your callback. Pros: no context switches, no buffer duplications. Cons: if you code takes too long it hogs the processing of incoming frames from the server.
* ConsumeMode.ParallelWithBufferCopy: takes a copy of the message body, and calls your callback from the threadpool. Pros: robust. Cons: buffer duplication = GC; no order guarantees. also more context switches.
* ConsumeMode.SerializedWithBufferCopy: takes a copy of the message body, and calls your callback from a bg thread - one thread per Consumer. Pros: robust and order guarantees. Cons: buffer duplication = GC. also more context switches.
* ~~ConsumeMode.ParallelWithReadBarrier~~: experimental.

```C#

Task<string> BasicConsume(ConsumeMode mode, IQueueConsumer consumer,
			string queue, string consumerTag, bool withoutAcks, bool exclusive,
			IDictionary<string, object> arguments, bool waitConfirmation)

Task<string> BasicConsume(ConsumeMode mode, Func<MessageDelivery, Task> consumer,
			string queue, string consumerTag, bool withoutAcks, bool exclusive,
			IDictionary<string, object> arguments, bool waitConfirmation)
```

Note: MessageDelivery implements IDisposable, and is disposed automatically. If you're saving it for later for any reason you need to 
either call SafeClone or set TakenOver=true. If the mode = SingleThreaded, TakenOver is ignored and the stream pointer will move to 
the next frame, so SafeClone is your friend.

##### RPC

Two helpers are offered:

```C#

Task<RpcHelper> CreateRpcHelper(ConsumeMode mode, int? timeoutInMs, int maxConcurrentCalls = 500);
Task<RpcAggregateHelper> CreateRpcAggregateHelper(ConsumeMode mode, int? timeoutInMs, int maxConcurrentCalls = 500);

```

These helpers implement the boilerplate code of setting up a temporary queue, sending messages with correct CorrelationId and ReplyTo sets, 
and processing replies, while also respecting timeouts. It also support recovery scenarios. 

It does all that with the minimum amount of gen0 allocations and is (with a few exceptions) lock free.

```C#

var helper = await channel.CreateRpcHelper(ConsumeMode.SingleThread, timeoutInMs: 100);
using(var reply = await helper.Call(exchangeName, routing, properties, body)) 
{
	...
}

```


##### Improvements / TODO

Mostly focused on decreasing allocs. 

* Support a Publish overload that takes a delegate for writing to the stream. This would save the upfront buffer allocation. The issue here is that
  the size of the frame body is written before the body content. We would have to call the user code, let it write to the stream, move backwards 
  and write the size. Even harder if the content is larger than the max frame size. OTOH way less GC allocs (fixed delegate vs unbounded buffer) 




