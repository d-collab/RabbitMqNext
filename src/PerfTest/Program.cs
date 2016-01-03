namespace PerfTest
{
	using System;
	using System.Threading.Tasks;
	using RabbitMqNext;

	public class Program
    {
	    public static void Main()
	    {
		    var t = Start();

		    t.Wait();

			Console.WriteLine("All done");
	    }

		private static async Task Start()
		{
			try
			{
				var conn = await new ConnectionFactory().Connect("localhost", vhost: "clear_test");

				Console.WriteLine("[Connected]");

				var newChannel = await conn.CreateChannel();

				Console.WriteLine("[channel created] " + newChannel.ChannelNumber);

				await newChannel.BasicQos(0, 150, false);

				await newChannel.ExchangeDeclare("test_ex", "direct", true, false, null, true);

				var qInfo = await newChannel.QueueDeclare("queue1", false, true, false, false, null, true);

				Console.WriteLine("[qInfo] " + qInfo);

				await newChannel.QueueBind("queue1", "test_ex", "routing1", null, true);

				var buffer = new byte[]
				{
					1, 2, 3, 4, 5, 6, 7, 8, 9, 10
				};

				await
					newChannel.BasicPublish("test_ex", "routing1", false, false, new BasicProperties()
					{
						Type = "type1", DeliveryMode = 2, 
					}, new ArraySegment<byte>(buffer));

			}
			catch (AggregateException ex)
			{
				Console.WriteLine("[Captured error] " + ex.Flatten().Message);
			}
			catch (Exception ex)
			{
				Console.WriteLine("[Captured error 2] " + ex.Message);
			}
		}
    }
}
