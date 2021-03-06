﻿
using Microsoft.Azure.Devices.Client;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using System.Diagnostics;
using System.Threading;

namespace Daenet.Iot
{
    public class IotHubConnector : IIotApi
    {
        /// <summary>
        /// Message as serialized for IotHub and original message s sobject.
        /// </summary>
        private List<Tuple<Microsoft.Azure.Devices.Client.Message, object>> m_SensorMessages = new List<Tuple<Microsoft.Azure.Devices.Client.Message, object>>();

        /// <summary>
        /// Number of retries when sending message(s).
        /// </summary>
        private int m_NumOfRetries = 5;

        /// <summary>
        /// Specifies how many messages will be acumulated, before batch of
        /// messages will be sent to IoTHub.
        /// </summary>
        private int m_NumOfMessagesPerBatch = 1;

        /// <summary>
        /// Action which can be used to serialize sensor Event to Message.
        /// </summary>
        private Func<object, Microsoft.Azure.Devices.Client.Message> m_SerializationFunction;


        /// <summary>
        /// Invoked when an error ocurres while sending message.
        /// </summary>
        private Action<Exception, IList<object>, int> m_OnRetryCallback;

        private DeviceClient m_DeviceClient;


        public string Name
        {
            get
            {
                string n = m_DeviceClient == null ? "" : Enum.GetName(typeof(TransportType), m_DeviceClient.TransportTypeInUse);
                return $"IotHubConnector-{n}";
            }
        }

        public async Task Open(Dictionary<string, object> args)
        {
            await Task.Run(() =>
            {
                if (args.ContainsKey("NumOfMessagesPerBatch"))
                    m_NumOfMessagesPerBatch = (int)args["NumOfMessagesPerBatch"];

                if (args.ContainsKey("SerializerAction"))
                    m_SerializationFunction = jsonSerializeFunc;

                if (args.ContainsKey("OnRetryCallback"))
                    m_OnRetryCallback = onRetryCallback;

                if (args.ContainsKey("NumOfRetries"))
                    m_NumOfRetries = (int)args["NumOfRetries"];


                string connStr = null;
                string deviceId = null;

                if (args != null)
                {
                    if (args.ContainsKey("ConnStr"))
                        connStr = (string)args["ConnStr"];
                    else
                        throw new Exception("IoTHub connection string must be provided.");

                    if (connStr.Contains("DeviceId") == false)
                    {
                        if (args.ContainsKey("DeviceId"))
                        {
                            deviceId = (string)args["DeviceId"];
                            m_DeviceClient = DeviceClient.CreateFromConnectionString(connStr, deviceId, TransportType.Http1);
                        }
                        else
                            throw new Exception("DeviceId must be provided in argument list or in connection string.");
                    }
                    else
                        m_DeviceClient = DeviceClient.CreateFromConnectionString(connStr, TransportType.Http1);
                }
            });
        }

        #region Default Services

        /// <summary>
        /// Default serislization function is JSON with UTF8 encoding.
        /// </summary>
        /// <param name="sensorMessage"></param>
        /// <returns></returns>
        private static Microsoft.Azure.Devices.Client.Message jsonSerializeFunc(object sensorMessage)
        {
            var messageString = JsonConvert.SerializeObject(sensorMessage);

            var message = new Microsoft.Azure.Devices.Client.Message(Encoding.UTF8.GetBytes(messageString));

            return message;
        }

        private static void onRetryCallback(Exception ex, IList<object> messages, int currentRetry)
        {
            if (ex is AggregateException)
                Debug.WriteLine($"Error: {ex.InnerException.GetType().Name}, {ex.InnerException.Message}, msgId:{messages.Count}, Current retry: {currentRetry}");
            else
                Debug.WriteLine($"Error: {ex.GetType().Name}, {ex.Message}, msgId:{messages.Count}, Current retry: {currentRetry}");

        }
        #endregion


        /// <summary>
        /// Starts message receiver loop.
        /// </summary>
        /// <param name="onReceiveMsg"></param>
        /// <param name="cancelationToken"></param>
        /// <param name="args"></param>
        /// <returns></returns>
        public Task OnMessage(Func<object, bool> onReceiveMsg, CancellationToken cancelationToken, Dictionary<string, object> args = null)
        {
            TimeSpan timeout = TimeSpan.FromMilliseconds(60000);

            if (args != null)
            {
                if (args.ContainsKey("TimeoutInMs"))
                {
                    timeout = TimeSpan.FromMilliseconds((int)args["TimeoutInMs"]);
                }
            }

           return Task.Run(() => {

                while (cancelationToken.IsCancellationRequested == false)
                {
                   try
                   {
                       var msg = m_DeviceClient.ReceiveAsync().Result;

                       bool completionState = (bool)onReceiveMsg?.Invoke(msg == null ? null : msg.GetBytes());

                       if (msg != null)
                       {
                           if (completionState)
                               m_DeviceClient.CompleteAsync(msg).Wait();
                           else
                               m_DeviceClient.AbandonAsync(msg).Wait();
                       }
                       else
                           Task.Delay(timeout).Wait();
                   }
                   catch (Exception ex)
                   {
                       Debug.WriteLine(ex.Message);
                   }
               }

            }, cancelationToken);        
      
        }

        public void OnSendAcknowledgeResult(Action<string, Exception> onMsgSendResult, Dictionary<string, object> args = null)
        {
            throw new NotImplementedException();
        }
        

        public void RegisterAcknowledge(Action<string, Exception> onAcknowledgeReceived)
        {
            throw new NotImplementedException();
        }


        /// <summary>
        /// Sends telemetry message to IoTHub.
        /// </summary>
        /// <param name="sensorMessage"></param>
        /// <param name="onSuccess"></param>
        /// <param name="onError"></param>
        /// <param name="args"></param>
        /// <returns></returns>
        public async Task SendAsync(object sensorMessage,
                                    Action<IList<object>> onSuccess = null,
                                    Action<IList<object>, Exception> onError = null,
                                    Dictionary<string, object> args = null)
        {
            await SendAsync(new List<object> { sensorMessage }, onSuccess, onError, args);
        }

        
        /// <summary>
        /// Sends telemetry message to IoTHub.
        /// </summary>
        /// <param name="sensorMessages"></param>
        /// <param name="onSuccess"></param>
        /// <param name="onError"></param>
        /// <param name="args"></param>
        /// <returns></returns>
        public async Task SendAsync(IList<object> sensorMessages,
            Action<IList<object>> onSuccess = null,
            Action<IList<object>, Exception> onError = null,
            Dictionary<string, object> args = null)
        {
            try
            {
                foreach (var msg in sensorMessages)
                {
                    m_SensorMessages.Add(new Tuple<Microsoft.Azure.Devices.Client.Message, object>(jsonSerializeFunc(msg), msg));
                }

                if (m_SensorMessages.Count >= m_NumOfMessagesPerBatch)
                {
                    int retries = 0;

                    
                    while (retries < m_NumOfRetries)
                    {
                        try
                        {
                            var messagesToSend = m_SensorMessages.Select(m => m.Item1);

                            await m_DeviceClient.SendEventBatchAsync(messagesToSend);

                            Debug.WriteLine($"Sent {m_SensorMessages.Count} events to cloud.");

                            try
                            {
                                onSuccess?.Invoke(new List<object>(m_SensorMessages.Select(m => m.Item2)));
                            }
                            catch (Exception callerException)
                            {
                                // This ensures that error will be thrown without retry.
                                retries = m_NumOfRetries + 1;
                                throw callerException;
                            }

                            m_SensorMessages.Clear();

                            break;
                        }                       
                        catch (Exception ex)
                        {
                            Debug.WriteLine("Warning sending to hub!" + ex.Message);
                            retries++;

                            if (retries >= m_NumOfRetries)
                            {
                                var msgToSend = m_SensorMessages.Select(m => m.Item1);
                                onError?.Invoke(new List<object>(m_SensorMessages), ex);
                            }
                            else
                            {
                                var newList = new List<Tuple<Message, object>>(m_SensorMessages);

                                m_SensorMessages.Clear();

                                foreach (var msgPair in newList)
                                {
                                    m_SensorMessages.Add(new Tuple<Microsoft.Azure.Devices.Client.Message, object>(jsonSerializeFunc(msgPair.Item2), msgPair.Item1));
                                }

                                await Task.Delay(1000);

                                m_OnRetryCallback?.Invoke(ex, m_SensorMessages.Select(m => m.Item2).ToList(), retries);
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                throw ex;
            }
        }

        /// <summary>
        /// Receives the command from IoTHub.
        /// </summary>
        /// <param name="onSuccess"></param>
        /// <param name="onError"></param>
        /// <param name="timeout"></param>
        /// <param name="args"></param>
        /// <returns></returns>
        public async Task ReceiveAsync(Func<object, bool> onSuccess = null,
            Func<Exception, bool> onError = null,
            int timeout = 60000,
            Dictionary<string, object> args = null)
        {
            Microsoft.Azure.Devices.Client.Message msg = null;

            try
            {
                msg = await m_DeviceClient.ReceiveAsync(TimeSpan.FromMilliseconds(timeout));

                bool completionState = (bool)onSuccess?.Invoke(msg == null ? null : msg.GetBytes());

                if (msg != null)
                {
                    if (completionState)
                        await m_DeviceClient.CompleteAsync(msg);
                    else
                        await m_DeviceClient.AbandonAsync(msg);
                }
            }
            catch (Exception ex)
            {
                bool completionState = (bool)onError?.Invoke(ex);

                if (msg != null)
                {
                    if (completionState)
                        await m_DeviceClient.CompleteAsync(msg);
                    else
                        await m_DeviceClient.AbandonAsync(msg);
                }
            }

            return;
        }
    }
}
