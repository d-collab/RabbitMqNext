# RabbitMqNext

Experimenting using TPL and async (completion ports/overlapped io) socket reads and buffer pools to see if a better rabbitmq client comes out of it. 

Not ready for production use.


Current stage: 

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
- 

- Control flow
- Heartbeat

- Connection/channels recovery / Programming model friendly

