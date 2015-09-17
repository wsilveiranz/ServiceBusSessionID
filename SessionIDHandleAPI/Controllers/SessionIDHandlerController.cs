using Microsoft.ServiceBus.Messaging;
using SessionIDHandleAPI.Models;
using Swashbuckle.Swagger.Annotations;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Web.Http;
using TRex.Metadata;
using Microsoft.Azure.AppService.ApiApps.Service;

namespace SessionIDHandleAPI.Controllers
{
    public class SessionIDHandlerController : ApiController
    {
        [Trigger(TriggerType.Poll, typeof(ServiceBusBasicMessageResult))]
        [Metadata("GetSessionMessages", "Gets all messages for the next available session.")]
        [HttpGet]
        public HttpResponseMessage GetSessionMessages(string triggerState)
        {
            List<ServiceBusBasicMessage> result = new List<ServiceBusBasicMessage>();
            var queuename = ConfigurationManager.AppSettings["QueueName"];
            var queueClient = QueueClient.Create(queuename);

            if (queueClient.GetMessageSessions().Count() > 0)
            {

                var nextSession = queueClient.AcceptMessageSession();
                var sessionId = nextSession.SessionId;
                BrokeredMessage message = null;
                while (true)
                {

                    //receive messages from Queue
                    message = nextSession.Receive(TimeSpan.FromSeconds(5));
                    if (message != null)
                    {
                        try
                        {
                            result.Add(new ServiceBusBasicMessage(message));
                            message.Complete();
                        }
                        catch (Exception ex)
                        {
                            message.DeadLetter("Failed Processing", ex.Message);
                        }

                    }
                    else
                    {
                        //no more messages in the queue
                        break;
                    }
                }

                queueClient.Close();

                return Request.EventTriggered(new ServiceBusBasicMessageResult(result),
                                           sessionId,
                                           TimeSpan.FromSeconds(30));
            }
            else
            {
                return Request.EventWaitPoll(retryDelay: null, triggerState: triggerState);
            }

        }

        [Metadata("GetAvailableSessions", "Gets a list of available sessions.")]
        [Route("api/SessionIDHandler/sessions")]
        public IEnumerable<string> GetAvailableSessions()
        {
            List<String> result = new List<string>();
            var queuename = ConfigurationManager.AppSettings["QueueName"];
            var queueClient = QueueClient.Create(queuename);

            var messageSessions = queueClient.GetMessageSessions();

            foreach (MessageSession messageSession in messageSessions)
            {
                result.Add(messageSession.SessionId);
            }
            
            queueClient.Close();

            return result;
        }


    }
}
