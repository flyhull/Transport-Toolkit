using Common_Support;
using Microsoft.Extensions.Logging;
using SixLabors.Fonts.Tables.AdvancedTypographic;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Transport_Support
{
    public class CachingHubProcessor : IHubProcessor
    {
        private readonly RoutedMessageAction action = RoutedMessageAction.CacheAndSend;
        private readonly byte[] fuzz = new byte[24];
        private readonly IDuplicateManager duplicateControl;
        private readonly IHubBanManager banControl;
        private readonly IRouteProvider routeProvider;
        private readonly IConnectionLookup connectionLookup;
        private readonly IExtractor extractor;
        private readonly IEmbedder embedder;
        private readonly ILogger<CachingHubProcessor> logger;
        private readonly ILoggerFactory loggerFactory;
        private readonly Int32 lag = -1 * TransportSupport.getMinimumDelayInSeconds();
        private readonly ITempFileManager tempFileManager;
        
        public CachingHubProcessor(ILoggerFactory loggerFactoryIn, ILogger<CachingHubProcessor> loggerIn, ITempFileManager tempFileManagerIn, IHubBanManager banControlIn, IDuplicateManager duplicateControlIn, IRouteProvider routeProviderIn, IConnectionLookup lookupIn, IEmbedder embedderIn, IExtractor extractorIn)
        {
            duplicateControl = duplicateControlIn;
            logger = loggerIn;
            connectionLookup = lookupIn;
            loggerFactory = loggerFactoryIn;
            routeProvider = routeProviderIn;
            banControl = banControlIn;
            extractor = extractorIn;
            embedder = embedderIn;
            tempFileManager = tempFileManagerIn;

            using (logger.BeginScope("Constructing Routing Hub Processor"))
            {
                try
                {
                    if (routeProvider.Valid)
                    {
                        logger.LogDebug("Routing is valid");
                    }
                    else
                    {
                        logger.LogCritical("Routing is invalid");
                    }

                    new Random((int)(DateTime.UtcNow.Ticks % Int32.MaxValue)).NextBytes(fuzz);

                    logger.LogDebug("Constructed");
                }
                catch (Exception ex)
                {
                    logger.LogCritical(ex, "Encountered Exception");
                }
            }
        }
        public RoutedMessage ProcessMessage(string base64In, string connectionId , string group, string url)
        {
            //hub processors create message with sender which may be ignored when the hub connection sends them later

            string sender = TransportSupport.getRandomSender();

            WayPoint destination = routeProvider.GetTo(group);

            RoutedMessage result = new RoutedMessage(loggerFactory, destination.HubUrl, sender, destination.Group, base64In, fuzz, action);

            using (logger.BeginScope("Processing Message " + result.Description))
            {               
                try
                {
                    ResultObject extraction = extractor.Extract(result.Payload.Base64String, DateTime.UtcNow.AddSeconds(lag), routeProvider.GetFrom(group), false, Image_Support.ImageOutputFormat.bytes);
                    
                    if (extraction.Worked)
                    {
                        if (duplicateControl.IsDuplicate(extraction.HashOfBytes, group))
                        {
                            result.RecordError(RoutedMessageStatus.duplicate);
                        }
                        else
                        {
                            
                            ResultObject temp = embedder.Embed(ref extraction, destination, DateTime.UtcNow, false, Image_Support.ImageOutputFormat.bytes);

                            result.UpdatePayload(temp);
                            
                            logger.LogDebug("Starting to write file for inbound message to be sent later");
                            result.UpdatePayload(tempFileManager.StoreBytes(result.Payload));
                            logger.LogInformation("File " + result.Payload.FileName + " written for inbound message " + result.Description + "was written to be sent later");
                        }
                    }
                    else
                    {
                        logger.LogDebug(extraction.Snapshot);
                        banControl.BanById(connectionId);
                        result.RecordError(RoutedMessageStatus.banned);
                    }

                    
                    
                }
                catch (Exception ex)
                {
                    logger.LogCritical(ex, "Encountered Exception");
                }
            }

            return result;
        }
    }
}
