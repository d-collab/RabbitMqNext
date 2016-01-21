# RabbitMqNext

Experimenting using [TPL](https://msdn.microsoft.com/en-us/library/dd460717%28v=vs.110%29.aspx) 
~~and sync (completion ports/overlapped io) socket reads~~ and buffer pools to see if a better 
rabbitmq client comes out of it. 

The goal is to drastically reduce contention and GC pauses. 

The way this is accomplished is two fold:

* Api invocations return future objects (in .net the idiomatic/de-facto way is to return a Task 
  which combined with async/await makes for a decent experience) so nothing is blocked on IO.

* writes and reads related to the socket are consumer/producer of ringbuffers thus one upfront 
  big allocation and that's it. The read/write loop happen in two dedicated threads.

**Not ready for production use.**


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

- ChannelFlow 
- Heartbeats 

- Connection/channels recovery / Programming model friendly
  Upon disconnection, try to reconnect and when successfull restore the channels and consumers

Not planning to support:
- Any authentication method besides plain/SSL
- multi body send (limit to framemax)
- multi body receive  (limit to framemax)


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
* ConsumeMode.ParallelWithBufferCopy: takes a copy of the message body, and calls your callback from the threadpool. Pros: robust. Cons: buffer duplication = GC. also more context switches.
* ~~ConsumeMode.ParallelWithReadBarrier~~: experimental.

```C#

Task<string> BasicConsume(ConsumeMode mode, QueueConsumer consumer,
			string queue, string consumerTag, bool withoutAcks, bool exclusive,
			IDictionary<string, object> arguments, bool waitConfirmation)

Task<string> BasicConsume(ConsumeMode mode, Func<MessageDelivery, Task> consumer,
			string queue, string consumerTag, bool withoutAcks, bool exclusive,
			IDictionary<string, object> arguments, bool waitConfirmation)
```
