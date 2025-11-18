using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.AspNetCore.Connections;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.Caching.Memory;
using System.Diagnostics;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Hosting;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.DependencyInjection;

namespace Transport_Support
{
    public class ListeningClient : IHostedService
    {

        private IRouteProvider Routing;
        private INetworkProcessor networkProcessor;
        private IFileProcessor fileProcessor;
        private readonly ILogger<ListeningClient> logger;
        private readonly ITempFileManager tempFileManager;
        private readonly IConfiguration configuration;
        private readonly ILoggerFactory loggerFactory;
        private readonly IClientBanManager clientBanManager;
        private IMemoryCache cache;
        private readonly string myName = "";
        private ClientConnection? connection;

        private readonly IMessageTracker messageTracker;

        public ListeningClient(ILoggerFactory loggerFactoryIn, IConfiguration configurationIn, ITempFileManager tempFileManagerIn, IRouteProvider routes, ILogger<ListeningClient> loggerIn, INetworkProcessor networkProcessorIn, IMemoryCache cacheIn, IMessageTracker messageTrackerIn, IClientBanManager clientBanManagerIn)
        {
            Routing = routes;
            logger = loggerIn;
            networkProcessor = networkProcessorIn;
            configuration = configurationIn;
            tempFileManager = tempFileManagerIn;
            cache = cacheIn;
            loggerFactory = loggerFactoryIn;
            fileProcessor = new DummyFileProcessor();
            messageTracker = messageTrackerIn;
            clientBanManager = clientBanManagerIn;

            using (logger.BeginScope("Constructing Listening Client"))
            {
                try
                {
                    myName = configuration["MyName"] ?? "unknown";

                    Console.Title = myName;

                    logger.LogDebug("ConstructedListening Client for " + myName + " in " + Environment.CurrentDirectory);
                }
                catch (Exception ex)
                {
                    logger.LogCritical(ex, "Encountered Exception");
                }
            }
        }
        public Task StartAsync(CancellationToken cancellingToken)
        {
            int patience = 20;

            using (logger.BeginScope("Starting Listening Client"))
            {
                try
                {
                    logger.LogDebug("Starting " + myName + " Listening Client");

                    if (Routing.Valid)
                    {
                        connection = new ClientConnection(loggerFactory, true, Routing.GetFrom().Group, Routing.GetFrom().HubUrl, tempFileManager, cache, fileProcessor, networkProcessor, messageTracker, clientBanManager);

                        if (connection.listener)
                        {
                            logger.LogDebug("Waiting to Connect");

                            while (!connection.IsConnected() && patience > 0)
                            {
                                Task.Delay(10000, cancellingToken).Wait(cancellingToken);
                                patience--;
                            }

                            if (connection.IsConnected())
                            {
                                logger.LogInformation("Started");
                            }
                            else
                            {
                                logger.LogError("Failed to Start Quickly Enough");
                            }
                        }
                        else
                        {
                            logger.LogCritical("Did not start because processor is mis configured");
                        }
                    }
                    else
                    {
                        logger.LogError("Did not start because route is invalid");
                    }
                }
                catch (Exception ex)
                {
                    logger.LogCritical(ex, "Did not start due to Exception");
                }
            }

            return Task.CompletedTask;
        }
        

        public Task StopAsync(CancellationToken cancellingToken)
        {
            int impatience = -20;

            using (logger.BeginScope("Stopping Listening Client"))
            {
                try
                {
                    logger.LogDebug("Stop requested");

                    if (connection == null)
                    {
                        logger.LogDebug("Connection is null, no need to Stop as Requested");
                    }
                    else
                    {
                        if (connection.IsConnected())
                        {
                            connection.Disconnect();

                            while (connection.IsConnected() && impatience < 1)
                            {
                                Task.Delay(1000, cancellingToken).Wait(cancellingToken);
                                impatience++;
                            }


                            if (connection.IsConnected())
                            {
                                logger.LogCritical("Did not Stop Quickly Enough");
                            }
                            else
                            {
                                logger.LogDebug("Stopped as Requested");
                            }
                        }
                        else
                        {
                            logger.LogDebug("Disconnected, no need to Stop as Requested");
                        }
                    }
                }
                catch (Exception ex)
                {
                    logger.LogCritical(ex, "Did not Stop properly due to Exception");
                }

            }

            return Task.CompletedTask;
        }
    }
}
