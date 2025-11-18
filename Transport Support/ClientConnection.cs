using Common_Support;
using Microsoft.AspNetCore.Connections;
using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Console;
using Microsoft.VisualBasic;
using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Transport_Support
{  
    public class ClientConnection : IClientConnection
    {
        //private string sender = new Guid().ToString();
        private readonly ILogger<ClientConnection> logger;
        private readonly IMemoryCache cache;
        private readonly INetworkProcessor networkProcessor;
        private readonly IFileProcessor fileProcessor;
        private readonly ITempFileManager tempFileManager;
        private readonly IClientBanManager clientBanManager;
        private HubConnection connection;
        private bool stopping = false;
        public readonly bool listener = false;
        private string fromGroup = string.Empty;
        private readonly IMessageTracker messageTracker;

        public bool IsConnected()
        {
            bool result = false;

            using (logger.BeginScope("Checking Client Connection"))
            {
                if (connection != null)
                {
                    result = (connection.State == HubConnectionState.Connected);
                }

                if (result)
                {
                    logger.LogDebug("Client is connected to Hub");
                }
                else
                {
                    logger.LogDebug("Client is not connected to Hub");
                }
            }
            return result;
        }

        public ClientConnection(ILoggerFactory loggerFactory, bool listenerIn, string fromGroupIn, string fromUrl, ITempFileManager tempFileManagerIn, IMemoryCache cacheIn, IFileProcessor fileProcessorIn, INetworkProcessor networkProcessorIn, IMessageTracker messageTrackerIn, IClientBanManager clientBanManagerIn)
        {            
            logger = loggerFactory.CreateLogger<ClientConnection>();
            cache = cacheIn;
            fromGroup = fromGroupIn;
            tempFileManager = tempFileManagerIn;
            fileProcessor = fileProcessorIn;
            networkProcessor = networkProcessorIn;
            messageTracker = messageTrackerIn;
            clientBanManager = clientBanManagerIn;

            using (logger.BeginScope("Starting Client Connection"))
            {
                logger.LogInformation("Starting to Create Connection to " + fromUrl);

                listener = listenerIn;

                connection = new HubConnectionBuilder()
                .WithUrl(new Uri(fromUrl))
                .WithAutomaticReconnect(new RandomRetryPolicy())
                .WithKeepAliveInterval(TimeSpan.FromSeconds(TransportSupport.getKeepAliveSeconds()))
                .WithServerTimeout(TimeSpan.FromSeconds(2 * TransportSupport.getKeepAliveSeconds()))
                .ConfigureLogging(logging =>
                {
                    logging.ClearProviders();
                    logging.AddSimpleConsole(logOptions =>
                    {
                        logOptions.IncludeScopes = true;
                        logOptions.SingleLine = false;
                        logOptions.UseUtcTimestamp = true;
                        logOptions.TimestampFormat = "HH:mm:ss ";
                    });
                    logging.SetMinimumLevel(LogLevel.Information);
                })

                .Build();

                try
                {
                    logger.LogDebug("Connection created, starting to configure");

                    if (listener)
                    {
                        logger.LogInformation("Connection will be configured as listener to group " + fromGroup);
                                                
                        connection.On<string, string>(ExposedClientAction.ReceiveMessage.ToString(), (sender, message)  =>
                        {
                            logger.LogDebug("RECEIVED MESSAGE FROM " + sender + " on connection " + connection.ConnectionId + " in state " + connection.State.ToString());

                            Console.WriteLine("***");
                            Console.WriteLine("*** ClientReceiptMessage1 - Received " + message + " from " + sender + " on " + DateTime.UtcNow.ToString("R"));
                            Console.WriteLine("***");

                            logger.LogDebug(ReceiveMessage(sender, message));

                        });

                    } 
                    else
                    {
                        logger.LogInformation("Connection will not be configured as listener");
                    }

                    connection.Reconnecting += error =>
                    {
                        logger.LogDebug("Connection Reconnecting");
                        return Task.CompletedTask;
                    };

                    connection.Reconnected += async connectionId =>
                    {
                        logger.LogDebug("Connection Reconnected");
                        if (listener)
                        {
                            logger.LogDebug("Adding to Group after Reconnection");
                            logger.LogDebug(await connection.InvokeAsync<string>(ExposedHubAction.AddMeToGroup.ToString(), fromGroup));

                            logger.LogInformation("Connection Re-added to " + fromGroup + " group");

                        }
                    };

                    connection.Closed += async (error) =>
                    {
                        if (!stopping)
                        {
                            logger.LogError(error, "Connection closed when it should not be");

                            Task.Delay(new Random().Next(3, 6) * 1000).Wait();

                            _ = ConnectWithRetryAsync(fromGroup, "Could Not Restart Connection");

                            await Task.Delay(0);  // added to suppress warning

                        }
                        else
                        {
                            logger.LogDebug("Connection Closed on purpose before Stopping");
                        }
                    };

                    _ = ConnectWithRetryAsync(fromGroup, "Could Not Start Connection");

                    logger.LogDebug("Connection Started");

                }
                catch (Exception ex)
                {
                    logger.LogCritical(ex, "Encountered Exception");
                }
            }
        }

        private string ReceiveMessage(string sender, string message)
        {
            string receivingResult = "TODO";

            using (logger.BeginScope("Processing Incoming Message from " + sender + " in  group " + fromGroup))
            {
                try
                {
                    if (clientBanManager.IsSenderBanned(sender))
                    {
                        receivingResult = "banned";
                        logger.LogInformation("Sender " + sender + " is banned");
                    }
                    else
                    {
                        logger.LogDebug(TransportSupport.describeMessageString(message));

                        RoutedMessage result = networkProcessor.ProcessInboundMessage(sender, message);

                        logger.LogDebug("Resulting message is " + result.Description);

                        result.DumpToLog("was received from hub and processed");

                        switch (result.Status)
                        {
                            case RoutedMessageStatus.banned:
                            case RoutedMessageStatus.duplicate:
                            case RoutedMessageStatus.wrongColor:
                                receivingResult = "Message was ignored because " + result.Status;
                                break;
                            case RoutedMessageStatus.wroteBytes:
                                switch (result.Action)
                                {
                                    case RoutedMessageAction.CacheAndSend:
                                        logger.LogDebug("Starting to write file for inbound message to be sent later");
                                        result.UpdatePayload(tempFileManager.StoreBytes(result.Payload));
                                        logger.LogDebug("File " + result.Payload.FileName + " written for inbound message " + result.Description + "was successfully written to be sent later");
                                        result.DumpToLog("can be cached");
                                        DateTime expiration = DateTime.UtcNow.AddSeconds(TransportSupport.getRandomDelayInSeconds());
                                        logger.LogDebug("Caching Routed Message for " + result.Payload.FileName);
                                        MemoryCacheEntryOptions cacheEntryOptions = new MemoryCacheEntryOptions()
                                        .SetPriority(CacheItemPriority.NeverRemove)
                                        .SetSize(1)
                                        .SetAbsoluteExpiration(expiration)
                                        .RegisterPostEvictionCallback(CacheItemRemoved, this);
                                        cache.Set(result.Payload.FileName, result, cacheEntryOptions);
                                        receivingResult = result.Payload.FileName + " will be forwarded at " + expiration.ToString("u");
                                        logger.LogDebug("File " + result.Payload.FileName + " written for inbound message " + result.Description + "was successfully written and will be sent later");
                                        break;
                                    case RoutedMessageAction.WriteForWatcher:
                                        logger.LogDebug("Starting to write file for inbound message to be sent by file watcher");
                                        result.UpdatePayload(tempFileManager.StoreBytes(result.Payload));
                                        receivingResult = result.Payload.FileName + " will be forwarded by the file watcher";
                                        logger.LogDebug("File " + result.Payload.FileName + " written for inbound message " + result.Description + "was successfully written to be sent by file watcher");
                                        break;
                                    default:
                                        receivingResult = "File written for inbound message " + result.Description + "was ignored because action is " + result.Action.ToString();
                                        break;
                                }
                                break;
                            case RoutedMessageStatus.wroteFile:
                                switch (result.Action)
                                {
                                    case RoutedMessageAction.DoNothing:
                                        receivingResult = "File " + result.Payload.FileName + " was successfully written for inbound message " + result.Description;
                                        break;
                                    default:
                                        receivingResult = "File written for inbound message " + result.Description + "was ignored because action is " + result.Action.ToString();
                                        break;
                                }
                                break;
                            case RoutedMessageStatus.readyToSend:
                                switch (result.Action)
                                {
                                    case RoutedMessageAction.SendImmediate:
                                        receivingResult = "Inbound message " + result.Description + " " + SendMessage(result).ToString();
                                        break;
                                    default:
                                        receivingResult = "Inbound message " + result.Description + "was ignored because action is " + result.Action.ToString();
                                        break;
                                }
                                break;                            
                            default:
                                logger.LogError("Could not process inbound message because " + result.Status.ToString());
                                if (result.Status == RoutedMessageStatus.seePayloadStatus)
                                {
                                    logger.LogError("Payload Status is " + result.Payload.Snapshot);
                                }
                                break;
                        }


                    }

                    logger.LogDebug(receivingResult);
                }
                catch (Exception ex)
                {
                    logger.LogCritical(ex, "Encountered Exception");

                }
            }

            return receivingResult;
        }

        public RoutedMessageStatus SendMessage(RoutedMessage message)
        {
            RoutedMessageStatus result = RoutedMessageStatus.none;

            using (logger.BeginScope("Sending Message " + message.messageId + " to " + message.group))
            {                
                try
                {                   
                    message.DumpToLog("was received from caller");

                    if (connection == null)
                    {
                        result = RoutedMessageStatus.disConnected;
                        logger.LogWarning("No connection available");
                    }
                    else
                    {
                        logger.LogDebug("Connection available");

                        if (message.Status == RoutedMessageStatus.readyToSend)
                        {
                            logger.LogDebug("Sending ready message " + TransportSupport.describeMessageString(message.Payload.Base64String) + " from " + message.sender + " to " + message.group);
                            string sendingResult = connection.InvokeAsync<string>(ExposedHubAction.SendMessageToGroup.ToString(), message.sender, message.Payload.Base64String, message.group).Result;
                            Console.WriteLine("***");
                            Console.WriteLine("*** ClientSendingMessage1 - " + message.sender + " sent " + message.Payload.Base64String + " to group " + message.group + " at " + message.hubUrl + " on " + DateTime.UtcNow.ToString("R"));
                            Console.WriteLine("***");
                            logger.LogInformation("Message " + message.Description + " " + sendingResult);
                            result = RoutedMessageStatus.messageSent;      
                        }
                        else
                        {
                            logger.LogError("Sending message failed because " + message.Status.ToString());
                            result = message.Status;
                        }
                    }
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Encountered Exception");
                    result = RoutedMessageStatus.exception;
                }

                logger.LogDebug("Returning " + result.ToString());
            }
            return result;
        }

        public RoutedMessageStatus ProcessFile( string filename)
        {
            RoutedMessageStatus result = RoutedMessageStatus.none;

            using (logger.BeginScope("Processing file " + filename ))
            {
                try
                {
                    if (string.IsNullOrEmpty(filename))
                    {
                        result = RoutedMessageStatus.fileNameMissing;
                    }
                    else
                    {
                        FileInfo fi = new FileInfo(filename);

                        if (fi.Exists)
                        {
                            if (fi.Length > 0)
                            {
                                RoutedMessage message = fileProcessor.ProcessOutboundFile(filename);

                                logger.LogDebug("File " + filename + " is being processed as message id " + message.messageId);

                                switch (message.Action)
                                {
                                    case RoutedMessageAction.SendImmediate:
                                        if (message.Status == RoutedMessageStatus.readyToSend)
                                        {
                                            logger.LogDebug("Processing succeded");
                                            result = SendMessage(message);
                                            logger.LogDebug(result.ToString());
                                            if (result == RoutedMessageStatus.messageSent && messageTracker.Tracking)
                                            {
                                                bool replyOkay = messageTracker.Sent(message.contentHash, filename);

                                                if (!replyOkay)
                                                {
                                                    result = RoutedMessageStatus.sentButCouldNotStartTracking;
                                                }                                                
                                            }
                                        }
                                        else
                                        {
                                            logger.LogError("Processing failed with " + message.Status.ToString());

                                            if (message.Status == RoutedMessageStatus.seePayloadStatus)
                                            {
                                                logger.LogError("Payload Status is " + message.Payload.Snapshot);
                                            }
                                            result = message.Status;
                                        }
                                        break;
                                    case RoutedMessageAction.DoNothing:
                                        result = message.Status;
                                        logger.LogDebug("No further processing is requested for " + filename + " with action of " + message.Action.ToString());
                                        break;
                                    default:
                                        logger.LogDebug("Processing for " + filename + " has unexpected action of " + message.Action.ToString());
                                        result = RoutedMessageStatus.unExpected;
                                        break;
                                }
                            }
                            else
                            {
                                result = RoutedMessageStatus.fileEmpty;
                            }
                        }
                        else
                        {
                            result = RoutedMessageStatus.fileMissing;
                        }
                    }
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Encountered Exception");
                    result = RoutedMessageStatus.exception;
                }

                logger.LogDebug("Returning " + result.ToString());
            }
            return result;
        }

        public RoutedMessageStatus BanSender(string sender)
        {
            RoutedMessageStatus result = RoutedMessageStatus.none;

            using (logger.BeginScope("Banning user " + sender))
            {
                try
                {
                    if (clientBanManager.IBan)
                    {
                        clientBanManager.BanBySender(sender);
                        result = RoutedMessageStatus.banned;
                        logger.LogDebug("Local ban completed");
                    }
                    else
                    {
                        if (connection == null)
                        {
                            result = RoutedMessageStatus.disConnected;
                            logger.LogWarning("No connection available");
                        }
                        else
                        {
                            logger.LogDebug("Connection available");
                            logger.LogDebug(connection.InvokeAsync<string>(ExposedHubAction.BanUser.ToString(), sender).Result);
                            result = RoutedMessageStatus.banned;
                            logger.LogDebug("Requesting ban completed");
                        }
                    }
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Encountered Exception");
                    result = RoutedMessageStatus.exception;
                }

                logger.LogDebug("Returning " + result.ToString());
            }
            return result;
        }

        public async void Disconnect()
        {
            using (logger.BeginScope("Stopping Client Connection"))
            {
                try
                {
                    stopping = true;
                    logger.LogDebug("Connection Stopping");
                    if (!(connection== null))
                    {
                        await connection.StopAsync();
                    }                    

                    while (IsConnected())
                    {
                        await Task.Delay(1000);
                    }
                    logger.LogDebug("Connection Stopped");
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Encountered Exception");
                }
            }
        }
        
        private async Task ConnectWithRetryAsync(string group, string msg)
        {
            Int32 patience = 10;
            using (logger.BeginScope("Connecting with retry"))
            {
                while (!IsConnected() && (patience > 0))
                {


                    try
                    {
                        logger.LogDebug("Trying to connect");
                        _ = connection.StartAsync();
                    }
                    catch (Exception ex)
                    {
                        logger.LogDebug(ex, "Failed to connect because of exception");
                    }

                    if (IsConnected())
                    {
                        break;
                    }
                    else
                    {
                        logger.LogDebug("Waiting to retry");
                        Task.Delay(new Random().Next(3, 6) * 1000).Wait();
                        patience--;
                        logger.LogDebug("Waited, will retry");
                    }
                }

                if (IsConnected())
                {
                    logger.LogDebug("Connection Started");
                    if (listener)
                    {
                        logger.LogDebug("Adding to Group after Starting");
                        logger.LogDebug(await connection.InvokeAsync<string>(ExposedHubAction.AddMeToGroup.ToString(), group));
                        logger.LogInformation("Connection added to " + group + " group");
                    }
                }
                else
                {
                    logger.LogCritical(msg);
                }
            }
        }

        private static void CacheItemRemoved(object identifier, object? cachedMessage, EvictionReason evictionReason, object? state)
        {
            try
            {
                if (state == null)
                {
                    Console.WriteLine("ERROR: Client Connection missing so we cannot log eviction of " + (string)identifier);
                }
                else
                {
                    ClientConnection cameFrom = (ClientConnection)state;

                    using (cameFrom.logger.BeginScope("Processing eviction of message " + (string)identifier + " from cache"))
                    {
                        if (cachedMessage == null)
                        {
                            cameFrom.logger.LogDebug("Description of " + (string)identifier + " did not survive evicted from cache because " + evictionReason.ToString());
                        }
                        else
                        {
                            RoutedMessage recoveredMessage = (RoutedMessage)cachedMessage;

                            recoveredMessage.DumpToLog("was retrieved from cache");

                            cameFrom.logger.LogDebug(recoveredMessage.Description + " evicted from cache because " + evictionReason.ToString());

                            cameFrom.logger.LogDebug("Trying to send " + recoveredMessage.Description + " to " + recoveredMessage.group);

                            try
                            {
                                ResultObject msgToSend = cameFrom.tempFileManager.GetBase64(recoveredMessage.Payload.FileName);

                                recoveredMessage.UpdatePayload(msgToSend);

                                cameFrom.logger.LogDebug("Inbound message " + recoveredMessage.Description + " sent with " + cameFrom.SendMessage(recoveredMessage).ToString());                                
                            }
                            catch (Exception ex)
                            {
                                cameFrom.logger.LogError(ex, "Encountered Exception sending " + recoveredMessage.Description + " after delay");
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("ERROR: Encountered exception " + ex.ToString() + " while processing eviction of " + (string)identifier);
            }
        }
    }

    public class RandomRetryPolicy : IRetryPolicy
    {
        private readonly Random _random = new Random();

        public TimeSpan? NextRetryDelay(RetryContext retryContext)
        {
            // If we've been reconnecting for less than 60 seconds so far,
            // wait between 0 and 10 seconds before the next reconnect attempt.
            if (retryContext.ElapsedTime < TimeSpan.FromSeconds(60))
            {
                return TimeSpan.FromSeconds(_random.NextDouble() * 10);
            }
            else
            {
                // If we've been reconnecting for more than 60 seconds so far, stop reconnecting.
                return null;
            }
        }
    }    
}
