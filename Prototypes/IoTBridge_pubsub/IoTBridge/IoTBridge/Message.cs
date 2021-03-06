﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace IotBridge
{
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using System.IO;
    using System.Net;


    public class Message
    {
        // readonly MessagingClient client;
        private object Body { get; set; }
        private Stream BodyStream { get; set; }

        public string ContentType { get; set; }
        public string CorrelationId { get; set; }
        public int DeliveryCount { get; set; }
        public long EnqueuedSequenceNumber { get; set; }
        public DateTime EnqueuedTimeUtc { get; set; }
        public DateTime ExpiresAtUtc { get; internal set; }
        public bool ForcePersistence { get; set; }
        public bool IsBodyConsumed { get; set; }
        public string Label { get; set; }
        public DateTime LockedUntilUtc { get; internal set; }
        public Guid LockToken { get; internal set; }
        public string MessageId { get; set; }
        public string PartitionKey { get; set; }
        public Dictionary<string, object> Properties { get; set; }
        public string ReplyTo { get; set; }
        public string ReplyToSessionId { get; set; }
        public DateTime ScheduledEnqueueTimeUtc { get; set; }
        public long SequenceNumber { get; set; }
        public string SessionId { get; set; }
        public long Size { get; internal set; }

        public TimeSpan TimeToLive { get; set; }
        public string To { get; set; }
        public string ViaPartitionKey { get; set; }

        public Message()
        {
            initializeMessage();
        }
        public Message(object serializableObject)
        {
            initializeMessage();
            this.Body = serializableObject;

        }

        public Message(Stream BodyStream)
        {
            initializeMessage();
            this.BodyStream = BodyStream;
        }

        private void initializeMessage()
        {
            Body = null;
            BodyStream = null;
            ContentType = null;
            CorrelationId = null;
            DeliveryCount = 0;
            EnqueuedSequenceNumber = 0;
            EnqueuedTimeUtc = new DateTime();
            ExpiresAtUtc = new DateTime();
            ForcePersistence = false;
            IsBodyConsumed = false;
            Label = null;
            LockedUntilUtc = new DateTime();
            LockToken = new Guid();
            MessageId = null;
            PartitionKey = null;
            Properties = new Dictionary<string, object>();
            ReplyTo = null;
            ReplyToSessionId = null;
            ScheduledEnqueueTimeUtc = new DateTime();
            SequenceNumber = 0;
            SessionId = null;
            Size = 0;

            TimeToLive = new TimeSpan();
            To = null;
            ViaPartitionKey = null;

        }
        public T GetBody<T>()
        {

            if (this.Body != null && typeof(T) == typeof(object))
            {
                return (T)Body;
            }
            else if (BodyStream != null && typeof(T) == typeof(Stream))
            {
                return (T)(object)BodyStream;
            }
            else
            {
                return default(T);
            }

        }
    }
}
