using Microsoft.ServiceBus.Messaging;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using ThirdPartyPayloads;

namespace ServiceBusSenderClient
{
    
    class Program
    {

        static void Main(string[] args)
        {
            Console.WriteLine("Press [Y] key to generate sample messages, or any other key to exit.");
            if (Console.ReadKey().Key == ConsoleKey.Y)
            {
                var queuename = ConfigurationManager.AppSettings["QueueName"];
                int sessionprefix = 0;
                while (sessionprefix < 10)
                {
                    sessionprefix++;
                    var sessionid = String.Format("{0}-{1}", queuename, sessionprefix.ToString("##"));

                    Console.WriteLine(String.Format("Sending message to queue {0} with sessionid {1} started.", queuename, sessionid));
                    SendMessages(queuename, sessionid);
                    Console.WriteLine(String.Format("Sending message to queue {0} with sessionid {1} completed.", queuename, sessionid));
                }
                //ReceiveMessages(queuename);

                Console.ReadKey();
            }
        }

        private static void SendMessages(string QueueName, string sessionid)
        {
            var queueClient = QueueClient.Create(QueueName);

            List<BrokeredMessage> messageList = new List<BrokeredMessage>();

            messageList.Add(CreateSampleMessage(sessionid, "1", "First message information"));
            messageList.Add(CreateSampleMessage(sessionid, "2", "Second message information"));
            messageList.Add(CreateSampleMessage(sessionid, "3", "Third message information"));

            Console.WriteLine("\nSending messages to Queue...");

            foreach (BrokeredMessage message in messageList)
            {
                while (true)
                {
                    try
                    {
                        queueClient.Send(message);
                    }
                    catch (MessagingException e)
                    {
                        if (!e.IsTransient)
                        {
                            Console.WriteLine(e.Message);
                            throw;
                        }
                        else
                        {
                            HandleTransientErrors(e);
                        }
                    }
                    Console.WriteLine(string.Format("Message sent: Id = {0}, Body = {1}", message.MessageId, message.GetBody<string>()));
                    break;
                }
            }
        }

        private static void ReceiveMessages(string queuename)
        {
            Console.WriteLine("\nRInitializing Queue...");
            var queueClient = QueueClient.Create(queuename);
            var nextSession = queueClient.AcceptMessageSession();
            Console.WriteLine("\nReceiving message from Queue...");
            BrokeredMessage message = null;
            while (true)
            {
                try
                {
                    //receive messages from Queue
                    message = nextSession.Receive(TimeSpan.FromSeconds(5));
                    if (message != null)
                    {
                        Console.WriteLine(string.Format("Message received: Sessionid = {0}, Id = {1}, Body = {2}", message.SessionId, message.MessageId, message.GetBody<string>()));
                        // Further custom message processing could go here...
                        message.Complete();
                    }
                    else
                    {
                        //no more messages in the queue
                        break;
                    }
                }
                catch (MessagingException e)
                {
                    if (!e.IsTransient)
                    {
                        Console.WriteLine(e.Message);
                        throw;
                    }
                    else
                    {
                        HandleTransientErrors(e);
                    }
                }
            }
            queueClient.Close();
        }

        private static BrokeredMessage CreateSampleMessage(string sessionid, string messageId, string messageBody)
        {
            var sampleMessage = new SampleMessage();
            sampleMessage.IntegerProperty = new Random().Next();
            sampleMessage.BooleanProperty = (sampleMessage.IntegerProperty % 2 == 0);
            sampleMessage.StringProperty = messageBody;
            sampleMessage.SomeSubClass = new SubMessage()
            {
                SomeIntValue = new Random().Next(),
                SomStringValue = messageBody
            };
        
            BrokeredMessage message = new BrokeredMessage(sampleMessage);
            message.MessageId = messageId;
            message.SessionId = sessionid;
            return message;
        }

        private static void HandleTransientErrors(MessagingException e)
        {
            //If transient error/exception, let's back-off for 2 seconds and retry
            Console.WriteLine(e.Message);
            Console.WriteLine("Will retry sending the message in 2 seconds");
            Thread.Sleep(2000);
        }
    }
}
