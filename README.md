# RabbitMqNext

Experimenting using [TPL](https://msdn.microsoft.com/en-us/library/dd460717%28v=vs.110%29.aspx) 
--and sync (completion ports/overlapped io) socket reads-- and buffer pools to see if a better 
rabbitmq client comes out of it. 

The goal is to drastically reduce contention and GC pauses. 

The way this is accomplished is two fold:

* Api invocations return future objects (in .net the idiomatic/de-facto way is to return a Task 
  which combined with async/await makes for a decent experience)

* writes and reads related to the socket are consumer/producer of ringbuffer thus one upfront 
  big allocation and that's it. The read/write loop happen in two dedicated thread.

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
- Publish confirm [Done] (_will wait the Task on BasicPublish until Ack from server, so it's slow and should be judiciously used_)

- ChannelFlow 
- Heartbeat 

- Connection/channels recovery / Programming model friendly


### Sample code


```C#
var conn = await ConnectionFactory.Connect(...);
var channel = await conn.CreateChannel();
await channel.BasicQos(0, 250, false);

```