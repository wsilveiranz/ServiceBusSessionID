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
using System.IO;

namespace SessionIDHandleAPI.Controllers
{
    public class SessionIDHandlerController : ApiController
    {
        /// <summary>
        /// Polling Trigger to get next session available and consume all associated messages.
        /// </summary>
        /// <param name="triggerState">Current State of the trigger State</param>
        /// <returns>Returns a response object containing an Array of Service Bus Messages.</returns>
        [Trigger(TriggerType.Poll, typeof(ServiceBusBasicMessageResult))]
        [Metadata("GetSessionMessages", "Gets all messages for the next available session.")]
        [HttpGet]
        [Route("api/SessionIDHandler/sessions/next/all")]
        public HttpResponseMessage GetSessionMessages(string triggerState)
        {
            List<ServiceBusBasicMessage> result = new List<ServiceBusBasicMessage>();
            var queuename = ConfigurationManager.AppSettings["QueueName"];
            var queueClient = QueueClient.Create(queuename);

            if (queueClient.GetMessageSessions().Count() > 0)
            {

                var nextSession = queueClient.AcceptMessageSession();
                var sessionId = nextSession.SessionId;

                Stream state = new MemoryStream();
                StreamWriter stateWriter = new StreamWriter(state);
                if (nextSession.GetState() != null)
                {
                    StreamReader reader = new StreamReader(nextSession.GetState());
                    stateWriter.Write(reader.ReadToEnd());
                }
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
                            
                            stateWriter.WriteLine(String.Format("Message {0} consumed.", message.MessageId));
                        //    message.Complete();
                        }
                        catch (Exception ex)
                        {
                            //temporary - was having trouble consuming messages from the queue.
                            message.DeadLetter("Failed Processing", ex.Message);
                        }

                    }
                    else
                    {
                        //no more messages in the queue
                        break;
                    }
                }
                stateWriter.Flush();
                nextSession.SetState(state);
                nextSession.Close();
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

        /// <summary>
        /// Polling Trigger to get next session available and consume next available message. Keep the lock open for processing..
        /// </summary>
        /// <param name="triggerState">Current State of the trigger State</param>
        /// <returns>Returns a basic Service Bus Basic Message</returns>
        [Trigger(TriggerType.Poll, typeof(ServiceBusBasicMessage))]
        [Metadata("GetNextSessionMessage", "Gets a single message for the next available session.")]
        [HttpGet]
        [Route("api/SessionIDHandler/sessions/next/single")]
        public HttpResponseMessage GetSessionNextMessage(string triggerState)
        {
            ServiceBusBasicMessage result = new ServiceBusBasicMessage();
            var queuename = ConfigurationManager.AppSettings["QueueName"];
            var queueClient = QueueClient.Create(queuename, ReceiveMode.PeekLock);

            if (queueClient.GetMessageSessions().Count() > 0)
            {

                var nextSession = queueClient.AcceptMessageSession();
                var sessionId = nextSession.SessionId;
                BrokeredMessage message = null;
                message = nextSession.Receive(TimeSpan.FromSeconds(5));
                if (message != null)
                {
                    try
                    {
                        result = new ServiceBusBasicMessage(message);
                        Stream state = (nextSession.GetState() == null) ? new MemoryStream() : nextSession.GetState();
                        
                        StreamWriter stateWriter = new StreamWriter(state);
                        stateWriter.WriteLine(String.Format("Message {0} consumed.", message.MessageId));
                        stateWriter.Flush();
                        nextSession.SetState(state);
                    //    message.Complete();
                    }
                    catch (Exception ex)
                    {
                        //temporary - was having trouble consuming messages from the queue.
                        message.DeadLetter("Failed Processing", ex.Message);
                    }

                }
                nextSession.Close();
                queueClient.Close();

                return Request.EventTriggered(result,
                                           sessionId,
                                           TimeSpan.FromSeconds(30));
            }
            else
            {
                return Request.EventWaitPoll(retryDelay: null, triggerState: triggerState);
            }

        }

        /// <summary>
        /// Gets the list of Messages Available
        /// </summary>
        /// <remarks>Created for Debug purposes</remarks>
        /// <returns>An string array of session IDs.</returns>
        [Metadata("GetAvailableSessions", "Gets a list of available sessions and state.")]
        [HttpGet]
        [Route("api/SessionIDHandler/sessions")]
        public SessionSummaryResult GetAvailableSessions()
        {
            List<SessionSummary> sessionsummaries = new List<SessionSummary>();
            var queuename = ConfigurationManager.AppSettings["QueueName"];
            var queueClient = QueueClient.Create(queuename);

            var messageSessions = queueClient.GetMessageSessions();

            foreach (MessageSession messageSession in messageSessions)
            {
                sessionsummaries.Add(new SessionSummary(messageSession));
            }
            
            queueClient.Close();

            var result = new SessionSummaryResult(sessionsummaries);
            return result;
        }

        [Metadata("CompleteMessage", "Completes a message after processing.")]
        [HttpPut]
        public HttpResponseMessage CompleteMessage(Guid token)
        {
            
            try
            {
                var queuename = ConfigurationManager.AppSettings["QueueName"];
                var queueClient = QueueClient.Create(queuename);
                queueClient.Complete(token);
        
                queueClient.Close();
                return new HttpResponseMessage(HttpStatusCode.OK);
            }
            catch (Exception ex)
            {
                var result = new HttpResponseMessage(HttpStatusCode.BadRequest);
                result.ReasonPhrase = String.Format("An error occurred while completing the message. {0}", ex.Message);
                return result;
            }
        }



    }
}
