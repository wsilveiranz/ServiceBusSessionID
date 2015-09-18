using Microsoft.ServiceBus.Messaging;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using System.Web;
using System.Xml;

namespace SessionIDHandleAPI.Models
{
    public class ServiceBusBasicMessage
    {
        //
        // Summary:
        //     Gets or sets the type of the content.
        //
        // Returns:
        //     The type of the content of the message body. This is a content type identifier
        //     utilized by the sender and receiver for application specific logic.
        //
        // Exceptions:
        //   T:System.ObjectDisposedException:
        //     Thrown if the message is in disposed state.
        public string ContentType { get; set; }
        //
        // Summary:
        //     Gets or sets the identifier of the correlation.
        //
        // Returns:
        //     The identifier of the correlation.
        //
        // Exceptions:
        //   T:System.ObjectDisposedException:
        //     Thrown if the message is in disposed state.
        public string CorrelationId { get; set; }
        //
        // Summary:
        //     Gets or sets the application specific label.
        //
        // Returns:
        //     The application specific label.
        //
        // Exceptions:
        //   T:System.ObjectDisposedException:
        //     Thrown if the message is in disposed state.
        public string Label { get; set; }
        //
        // Summary:
        //     Gets or sets the identifier of the message.
        //
        // Returns:
        //     The identifier of the message.
        //
        // Exceptions:
        //   T:System.ObjectDisposedException:
        //     Thrown if the message is in disposed state.
        //
        //   T:System.ArgumentException:
        //     Thrown if the message identifier is null or exceeds 128 characters in length.
        //
        // Summary:
        //     Gets the lock token assigned by Service Bus to this message.
        //
        // Returns:
        //     The lock token assigned by Service Bus to this message.
        //
        // Exceptions:
        //   T:System.ObjectDisposedException:
        //     Thrown if the message is in disposed state.
        //
        //   T:System.InvalidOperationException:
        //     Thrown if the message was not received from the ServiceBus.
        public Guid LockToken { get; set; }
        public string MessageId { get; set; }
        //
        // Summary:
        //     Gets the application specific message properties.
        //
        // Returns:
        //     The application specific message properties.
        //
        // Exceptions:
        //   T:System.ObjectDisposedException:
        //     Thrown if the message is in disposed state.
        public IDictionary<string, object> Properties { get; set; }
        //
        // Summary:
        //     Gets or sets the identifier of the session.
        //
        // Returns:
        //     The identifier of the session.
        //
        // Exceptions:
        //   T:System.ObjectDisposedException:
        //     Thrown if the message is in disposed state.
        public string SessionId { get; set; }

        public string Content { get; set; }

        public ServiceBusBasicMessage()
        {
        }
        public ServiceBusBasicMessage(BrokeredMessage message)
        {
            ContentType = message.ContentType;
            CorrelationId = message.CorrelationId;
            Label = message.Label;
            LockToken = message.LockToken;
            MessageId = message.MessageId;
            Properties = message.Properties;
            SessionId = message.SessionId;

            if (String.IsNullOrEmpty(ContentType))
                ContentType = "System.String";

            Type bodyType = Type.GetType(ContentType, true);
            var stream = message.GetBody<Stream>();
            DataContractSerializer serializer = new DataContractSerializer(bodyType);
            XmlDictionaryReader reader = XmlDictionaryReader.CreateBinaryReader(stream, XmlDictionaryReaderQuotas.Max);
            Content = JsonConvert.SerializeObject(serializer.ReadObject(reader));
        }
    }
    public class ServiceBusBasicMessageResult
    {
        public IEnumerable<ServiceBusBasicMessage> Messages { get; set; }

        public ServiceBusBasicMessageResult()
        { }

        public ServiceBusBasicMessageResult(List<ServiceBusBasicMessage> messages)
        {
            Messages = messages;
        }
    }
    public class SessionSummary
    {
        public string SessionId { get; set; }
        public String SessionState { get; set; }

        public SessionSummary()
        { }

        public SessionSummary(MessageSession session)
        {
            SessionId = session.SessionId;
            Stream state = session.GetState();
            if (state != null)
            {
                StreamReader reader = new StreamReader(state);
                SessionState = reader.ReadToEnd();
            }
            else
            {
                SessionState = String.Empty;
            }

        }       
    }
    public class SessionSummaryResult
    {
        public IEnumerable<SessionSummary> Sessions { get; set; }

        public SessionSummaryResult()
        {

        }

        public SessionSummaryResult(List<SessionSummary> sessions)
        {
            Sessions = sessions;
        }
    }

}