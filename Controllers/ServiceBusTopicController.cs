using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Web.Http;
using TRex.Metadata;
using Microsoft.ServiceBus.Messaging;
using System.Configuration;
using System.IO;
using Microsoft.ServiceBus;
using Microsoft.Azure.AppService.ApiApps.Service;

namespace ServiceBusTopic.Controllers
{
    public class ServiceBusTopicController : ApiController
    {
        private SubscriptionClient subscriptionClient;
        private NamespaceManager namespaceManager;
        private void InitializeSubscriptionClient(string topic, string subscription, string filter)
        {
            if (namespaceManager == null)
            {
                TokenProvider tokenProvider =TokenProvider.CreateSharedAccessSignatureTokenProvider(
                    ConfigurationManager.AppSettings["Microsoft.ServiceBus.AccountInfo.PolicyName"],
                    ConfigurationManager.AppSettings["Microsoft.ServiceBus.AccountInfo.Key"]);



                namespaceManager = new NamespaceManager(ConfigurationManager.AppSettings["Microsoft.ServiceBus.Address"],tokenProvider);
            }

            if (!namespaceManager.SubscriptionExists(topic, subscription))
            {

                namespaceManager.CreateSubscription(topic, subscription, new SqlFilter(filter));
            }

            if (subscriptionClient == null || subscriptionClient.Name != subscription)
            {
                subscriptionClient = SubscriptionClient.CreateFromConnectionString(ConfigurationManager.AppSettings["Microsoft.ServiceBus.ConnectionString"], topic, subscription);
            }
        }

        [Trigger(TriggerType.Poll, typeof(BrokeredMessage))]
        [Metadata("GetTopicMessageBySubscrptionAndFilter", "Get next available message in a topic subscription that matches a filter.")]
        [HttpGet]
        [Route("api/SessionIDHandler/sessions/next/all")]
        public async System.Threading.Tasks.Task<HttpResponseMessage> GetSessionMessages(string triggerState, string topic, string subscription, string filter)
        {
            InitializeSubscriptionClient(topic, subscription, filter);

            BrokeredMessage result;

                result = await subscriptionClient.ReceiveAsync(TimeSpan.FromSeconds(5));
            if (result != null)
            {
                result.Complete();
            
                return Request.EventTriggered(result,
                                           result.SequenceNumber.ToString(),
                                           TimeSpan.FromSeconds(30));
            }
            else
            {
                return Request.EventWaitPoll(retryDelay: null, triggerState: triggerState);
            }

        }
    }
}
