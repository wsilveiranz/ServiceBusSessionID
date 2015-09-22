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
using System.ComponentModel.DataAnnotations;

namespace SessionIDHandleAPI.Controllers
{
    public class SessionIDHandlerController : ApiController
    {

        private QueueClient queueClient = QueueClient.Create(ConfigurationManager.AppSettings["QueueName"], ReceiveMode.PeekLock);
        /// <summary>
        /// Polling Trigger to get next session available and consume all associated messages.
        /// </summary>
        /// <param name="triggerState">Current State of the trigger State</param>
        /// <returns>Returns a response object containing an Array of Service Bus Messages.</returns>
        [Trigger(TriggerType.Poll, typeof(ServiceBusBasicMessageResult))]
        [Metadata("GetSessionMessages", "Gets all messages for the next available session.")]
        [HttpGet]
        [Route("api/SessionIDHandler/sessions/next/all")]
        public async System.Threading.Tasks.Task<HttpResponseMessage> GetSessionMessages(string triggerState)
        {
            List<ServiceBusBasicMessage> result = new List<ServiceBusBasicMessage>();

            if ((await queueClient.GetMessageSessionsAsync()).Count() > 0)
            {
                
                var nextSession = await queueClient.AcceptMessageSessionAsync();
                
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
                    await nextSession.RenewLockAsync();
                    
                    //receive messages from Queue
                    message = await nextSession.ReceiveAsync(TimeSpan.FromSeconds(5));

                    if (message != null)
                    {
                        try
                        {
                            result.Add(new ServiceBusBasicMessage(message));
                            stateWriter.WriteLine(String.Format("Message {0} consumed.", message.MessageId));
                            message.Defer();
                           // message.Complete();

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
        public async System.Threading.Tasks.Task<HttpResponseMessage> GetSessionNextMessage()
        {
            ServiceBusBasicMessage result = new ServiceBusBasicMessage();


            if (queueClient.GetMessageSessions().Count() > 0)
            {

                var nextSession = queueClient.AcceptMessageSession(TimeSpan.FromSeconds(5));
                var sessionId = nextSession.SessionId;

                BrokeredMessage message = null;

                message = nextSession.Receive(TimeSpan.FromSeconds(5));
                if (message != null)
                {
                    try
                    {
                        result = new ServiceBusBasicMessage(message);
                        Stream state = new MemoryStream();
                        StreamWriter stateWriter = new StreamWriter(state);
                        if (nextSession.GetState() != null)
                        {
                            StreamReader reader = new StreamReader(nextSession.GetState());
                            stateWriter.Write(reader.ReadToEnd());
                        }
                        stateWriter.WriteLine(String.Format("Message {0} consumed.", message.MessageId));
                        stateWriter.Flush();
                        nextSession.SetState(state);
                        await System.Threading.Tasks.Task.Delay(TimeSpan.FromSeconds(50));
                        //message.Defer();
                        message.Complete();
                    }
                    catch (Exception ex)
                    {
                        //temporary - was having trouble consuming messages from the queue.
                        message.DeadLetter("Failed Processing", ex.Message);
                    }

                }
                nextSession.Close();


                return Request.EventTriggered(result,
                                           sessionId,
                                           TimeSpan.FromSeconds(30));
            }
            else
            {
                return Request.EventWaitPoll();
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

            var messageSessions = queueClient.GetMessageSessions();

            foreach (MessageSession messageSession in messageSessions)
            {
                sessionsummaries.Add(new SessionSummary(messageSession));
            }


            var result = new SessionSummaryResult(sessionsummaries);
            return result;
        }

        [Metadata("CompleteMessage", "Completes a message after processing.")]
        [SwaggerResponse(HttpStatusCode.OK, "Message Completed", typeof(BrokeredMessage))]
        [HttpPut]
        public HttpResponseMessage CompleteMessage(long sequenceNum, string sessionId)
        {
            var session = queueClient.AcceptMessageSession(sessionId);
            try
            {
                var message = session.Receive(sequenceNum);
                message.Complete();
                //session.Complete(token);

                session.Close();

                return Request.CreateResponse<BrokeredMessage>(HttpStatusCode.OK, message);
            }
            catch (Exception ex)
            {
                var result = new HttpResponseMessage(HttpStatusCode.BadRequest);
                result.ReasonPhrase = String.Format("An error occurred while completing the message. {0}", ex.Message);
                session.Close();
                return result;
            }
        }

    }
}
