//using Microsoft.AspNetCore.Mvc.Formatters;
using Common_Support;
using Microsoft.AspNetCore.Connections;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Razor.TagHelpers;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using SixLabors.ImageSharp.Processing.Processors.Filters;
using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using System.Timers;
using Transport_Support;

namespace Hubs
{

    public class UserNameProvider : IUserIdProvider
    {
        public string GetUserId(HubConnectionContext connection)
        {
            return TransportSupport.getSenderFromConnectionId(connection.ConnectionId);
        }
    }

    public class ChatHub : Hub, IChatHub
    {
        private readonly string myName = string.Empty;
        //private System.Threading.Timer aTimer;
        private readonly ILogger<ChatHub> logger;
        private readonly IHubProcessor hubProcessor;
        private readonly IConfiguration configuration;
        private readonly IConnectionLookup lookup;
        private readonly IMemoryCache cache;
        private readonly IHubBanManager banManager;
        private readonly ILoggerFactory loggerFactory;
        private readonly string myUrl = string.Empty;

        public ChatHub(ILoggerFactory loggerFactoryIn, ILogger<ChatHub> loggerIn, IConfiguration configurationIn, IHubProcessor hubProcessorIn, IMemoryCache cacheIn, IConnectionLookup lookupIn, IHubBanManager banManagerIn)
        {
            logger = loggerIn;
            configuration = configurationIn;
            banManager = banManagerIn;
            hubProcessor = hubProcessorIn;
            cache = cacheIn;
            lookup = lookupIn;
            loggerFactory = loggerFactoryIn;

            //aTimer = new System.Threading.Timer(new System.Threading.TimerCallback(OnTimedEvent), cache, 0, 1000);

            using (logger.BeginScope("Constructing Typed Hub"))
            {
                try
                {
                    myName = configuration["MyName"] ?? "unknown";
                    myUrl = configuration["Kestrel:Endpoints:MyHttpEndpoint:Url"] ?? "unknown";
                    logger.LogInformation(myName + " Active");
                    logger.LogInformation("Hub Constructed");
                }
                catch (Exception ex)
                {
                    logger.LogCritical(ex, "Encountered Exception");
                }
            }

        }

        public override async Task OnConnectedAsync()
        {
            using (logger.BeginScope("Adding connection " + Context.ConnectionId))
            {
                try
                {
                    var feature = Context.Features.Get<IHttpConnectionFeature>();
                    string Ip = String.Empty;
                    if (feature != null)
                    {
                        if (feature.RemoteIpAddress != null)
                        {
                            Ip = feature.RemoteIpAddress.ToString();
                        }
                        else
                        {
                            logger.LogInformation("Cannot get client's ip address");
                        }
                    }
                    else
                    {
                        logger.LogInformation("Cannot get client's connection info");
                    }

                    if (string.IsNullOrEmpty(Ip))
                    {
                        logger.LogInformation("Client's ip address is unknown, cannot check for ban");
                    }
                    else
                    {
                        logger.LogInformation("Client's ip address is " + Ip + "checking for ban");
                    }

                    if (banManager.IsIpBanned(Ip))
                    {
                        logger.LogInformation("Connection request ignored since client's ip is banned");
                    }
                    else
                    {
                        lookup.InsertConnection(Context.ConnectionId, Ip);
                        await base.OnConnectedAsync();
                        logger.LogInformation("Connection added");
                    }
                }
                catch (Exception ex)
                {
                    logger.LogCritical(ex, "Encountered Exception");
                }
            }
        }

        public override async Task OnDisconnectedAsync(Exception? exception)
        {
            using (logger.BeginScope("Removing connection " + Context.ConnectionId))
            {
                try
                {
                    lookup.RemoveConnection(Context.ConnectionId);
                    await base.OnDisconnectedAsync(exception);
                    logger.LogInformation("Connection removed");
                }
                catch (Exception ex)
                {
                    logger.LogCritical(ex, "Encountered Exception");
                }
            }
        }

        //private static void OnTimedEvent(Object? source)
        //{
        //    try
        //    {
        //        if (source == null)
        //        {
        //            Console.WriteLine("Cache is Missing");
        //        }
        //        else
        //        {
        //            IMemoryCache cache = (IMemoryCache)source;
        //            _ = cache.TryGetValue("NotHash", out _);
        //        }
        //    }
        //    catch (Exception ex)
        //    {
        //        Console.WriteLine("Encountered Exception " + ex.ToString() + " while checking cache");
        //    }
        //}

        public async Task<string> SendMessageToGroup( string nominalSender, string message, string group)
        {
            string sendingResult = "TODO";
           
            using (logger.BeginScope("Attempting to send message " + TransportSupport.describeMessageString(message) + " from nominal sender " + nominalSender + " to the " + group + " group for connection " + Context.ConnectionId))
            {
                try
                {
                    string sender = lookup.GetSenderByConnectionId(Context.ConnectionId);
                    logger.LogInformation("Actual sender is " + sender);

                    Console.WriteLine("***");
                    Console.WriteLine("*** HubReceiptMessage1 - Received " + message + " from " + sender + " to group " + group + " at " +  myUrl + "/chatHub on " + DateTime.UtcNow.ToString("R"));
                    Console.WriteLine("***");

                    if (banManager.IsIdBanned(Context.ConnectionId))
                    {
                        sendingResult = "Ignored since requester is banned";
                    }
                    else
                    {
                        RoutedMessage msg = hubProcessor.ProcessMessage(message, Context.ConnectionId, group, myUrl);

                        switch (msg.Status)
                        {
                            case RoutedMessageStatus.banned:
                            case RoutedMessageStatus.duplicate:
                            case RoutedMessageStatus.wrongColor:
                                sendingResult = "Message was ignored because " + msg.Status.ToString();
                                break;
                            case RoutedMessageStatus.wroteFile:
                                switch (msg.Action)
                                {                                   
                                    case RoutedMessageAction.CacheAndSend:
                                        logger.LogInformation("File " + msg.Payload.FileName + " written for inbound message " + msg.Description + "was successfully written to be sent later");
                                        msg.DumpToLog("can be cached");
                                        DateTime expiration = DateTime.UtcNow.AddSeconds(TransportSupport.getRandomDelayInSeconds());
                                        logger.LogDebug("Caching Routed Message for " + msg.Payload.FileName);
                                        MemoryCacheEntryOptions cacheEntryOptions = new MemoryCacheEntryOptions()
                                        .SetPriority(CacheItemPriority.NeverRemove)
                                        .SetSize(1)
                                        .SetAbsoluteExpiration(expiration)
                                        .RegisterPostEvictionCallback(CacheItemRemoved, Context.GetHttpContext());
                                        cache.Set(msg.Payload.FileName, msg, cacheEntryOptions);
                                        sendingResult = "Message " + msg.Description + " successfully sent";
                                        logger.LogInformation(sendingResult);
                                        logger.LogInformation(msg.Payload.FileName + " written for inbound message " + msg.Description + " will be forwarded at " + expiration.ToString("u"));
                                        break;
                                    default:
                                        sendingResult = "Inbound message " + msg.Description + " was ignored because action is " + msg.Action.ToString();
                                        break;
                                }
                                break;
                            case RoutedMessageStatus.readyToSend:
                                switch (msg.Action)
                                {
                                    case RoutedMessageAction.SendImmediate:
                                        logger.LogInformation("Inbound message " + msg.Description + " was successfully processed to " + msg.Action.ToString());
                                        msg.DumpToLog("Resulting message is");

                                        await Clients.Group(msg.group).SendAsync(ExposedClientAction.ReceiveMessage.ToString(), msg.sender, msg.Payload.Base64String);
                                        
                                        Console.WriteLine("***");
                                        Console.WriteLine("*** HubSendingMessage1 - " + msg.sender + " sent " + msg.Payload.Base64String + " to group " + msg.group + " at " + msg.hubUrl + " on " + DateTime.UtcNow.ToString("R"));
                                        Console.WriteLine("***");

                                        sendingResult = "Message " + msg.Description + " successfully sent";
                                        logger.LogInformation(sendingResult);
                                        break;                                    
                                    default:
                                        sendingResult = "Inbound message " + msg.Description + " was ignored because action is " + msg.Action.ToString();
                                        break;
                                }
                                break;
                            default:

                                if (msg.Status == RoutedMessageStatus.seePayloadStatus)
                                {
                                    sendingResult = "Could not process inbound message because payload Status is " + msg.Payload.Snapshot;
                                }
                                else
                                {
                                    sendingResult = "Could not process inbound message because message is " + msg.Status;
                                }
                                logger.LogError(sendingResult);
                                break;
                        }
                    }
                    //logger.LogInformation(sendingResult);
                }
                catch (Exception ex)
                {
                    logger.LogCritical(ex, "Encountered Exception");
                    sendingResult = ex.Message;
                }

            }

            return sendingResult;
        }
        public async Task<string> AddMeToGroup(string group)
        {
            string addingResult = "TODO";

            using (logger.BeginScope("Attempting to add connection " + Context.ConnectionId + " to the " + group + " group"))
            {
                try
                {
                    if (banManager.IsIdBanned(Context.ConnectionId))
                    {
                        addingResult = "Ignored since requester is banned";
                    }
                    else
                    {
                        await Groups.AddToGroupAsync(Context.ConnectionId, group);
                        addingResult = "Added connection " + Context.ConnectionId + " to group " + group;
                    }

                    logger.LogInformation(addingResult);
                }
                catch (Exception ex)
                {
                    logger.LogCritical(ex, "Encountered Exception");
                    addingResult = ex.Message;
                }
            }

            return addingResult;
        }
        public async Task<string> BanUser(string user)
        {
            string banningResult = "TODO";

            using (logger.BeginScope("Banning user " + user + " as requested by connection " + Context.ConnectionId))
            {
                try
                {
                    if (banManager.IBan)
                    {
                        banningResult = "Clients are not allowed to ban";
                    }
                    else
                    {
                        if (banManager.IsIdBanned(Context.ConnectionId))
                        {
                            banningResult = "Ignored since requester is banned";
                        }
                        else
                        {
                            if (banManager.IsIdBanned(Context.ConnectionId))
                            {
                                banningResult = "Ignored since requester is banned";
                            }
                            else
                            {
                                await Task.Delay(1);  // makes it async

                                if (banManager.BanBySender(user))
                                {
                                    banningResult = "Ban worked";
                                }
                                else
                                {
                                    banningResult = "Ban failed";
                                }
                            }
                        }
                    }
                    logger.LogInformation(banningResult);
                }
                catch (Exception ex)
                {
                    logger.LogCritical(ex, "Encountered Exception");
                    banningResult = ex.Message;
                }
            }

            return banningResult;

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
                    HttpContext myContext = (HttpContext)state;
                    var myHubContext = myContext.RequestServices.GetRequiredService <IHubContext<ChatHub>>();
                    ILoggerFactory loggerFactory = myContext.RequestServices.GetRequiredService<ILoggerFactory>();
                    ILogger<ChatHub> logger = loggerFactory.CreateLogger<ChatHub>();
                    ITempFileManager tempFileManager = myContext.RequestServices.GetRequiredService<ITempFileManager>();

                    using (logger.BeginScope("Processing eviction of message " + (string)identifier + " from cache"))
                    {
                        if (cachedMessage == null)
                        {
                            logger.LogError("Description of " + (string)identifier + " did not survive evicted from cache because " + evictionReason.ToString());
                        }
                        else
                        {
                            RoutedMessage recoveredMessage = (RoutedMessage)cachedMessage;

                            recoveredMessage.DumpToLog("was retrieved from cache");

                            logger.LogInformation(recoveredMessage.Description + " evicted from cache because " + evictionReason.ToString());

                            logger.LogInformation("Trying to send " + recoveredMessage.Description + " to " + recoveredMessage.group);

                            try
                            {
                                ResultObject msgToSend = tempFileManager.GetBase64(recoveredMessage.Payload.FileName);

                                recoveredMessage.UpdatePayload(msgToSend);
                                                                
                                myHubContext.Clients.Group(recoveredMessage.group).SendAsync(ExposedClientAction.ReceiveMessage.ToString(), recoveredMessage.sender, recoveredMessage.Payload.Base64String);

                                Console.WriteLine("***");
                                Console.WriteLine("*** HubSendingMessage2 - " + recoveredMessage.sender + " sent " + recoveredMessage.Payload.Base64String + " to group " + recoveredMessage.group + " at " + recoveredMessage.hubUrl + " on " + DateTime.UtcNow.ToString("R"));
                                Console.WriteLine("***");

                                logger.LogInformation("Inbound message " + recoveredMessage.Description + " sent ");
                            }
                            catch (Exception ex)
                            {
                                logger.LogError(ex, "Encountered Exception sending " + recoveredMessage.Description + " after delay");
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
}
