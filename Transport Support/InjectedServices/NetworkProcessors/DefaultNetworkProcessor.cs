using Common_Support;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using SixLabors.ImageSharp.PixelFormats;
using System;
using System.Buffers.Text;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Transport_Support
{
    public class DefaultNetworkProcessor : INetworkProcessor
    {
        private readonly RoutedMessageAction action = RoutedMessageAction.ReportError;
        private readonly byte[] fuzz = new byte[24];
        private readonly IDuplicateManager duplicateControl;
        private readonly IRouteProvider routeProvider;
        //private readonly ITempFileManager tempFileManager;
        private readonly IExtractor extractor;
        private readonly IEmbedder embedder;
        private readonly IUsage role;
        private readonly ILogger<DefaultNetworkProcessor> logger;
        private readonly ILoggerFactory loggerFactory;
        private readonly Int32 lag = -1 * TransportSupport.getMinimumDelayInSeconds();

             

        public DefaultNetworkProcessor(ILoggerFactory loggerFactoryIn, ILogger<DefaultNetworkProcessor> loggerIn, IDuplicateManager duplicateControlIn, IRouteProvider routeProviderIn,  ITempFileManager tempFileManagerIn, IEmbedder embedderIn, IExtractor extractorIn, IUsage roleIn)
        {
            duplicateControl = duplicateControlIn;
            logger = loggerIn;
            loggerFactory = loggerFactoryIn;
            routeProvider = routeProviderIn;
            //tempFileManager = tempFileManagerIn;
            role = roleIn;
            extractor = extractorIn;
            embedder = embedderIn;

            using (logger.BeginScope("Constructing Default Processor"))
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

                    if (role.ProgramPurpose == Purpose.Relay)
                    {
                        action = RoutedMessageAction.WriteForWatcher;
                    }
                    if (role.ProgramPurpose == Purpose.Reflector)
                    {
                        action = RoutedMessageAction.CacheAndSend;
                    }

                    logger.LogDebug("Constructed");
                }
                catch (Exception ex)
                {
                    logger.LogCritical(ex, "Encountered Exception");
                }
            }
        }

        
        public RoutedMessage ProcessInboundMessage(string sender, string message)
        {
            //network processors create message with sender which will be ignored when the client connection sends them later

            RoutedMessage result = new RoutedMessage(loggerFactory, routeProvider.GetTo().HubUrl, sender, routeProvider.GetTo().Group, message, fuzz, action);

            using (logger.BeginScope("Receiving Message " + TransportSupport.describeMessageString(message)))
            {
                try
                {
                    result.DumpToLog("received by Default Network Processor");

                    if (routeProvider.Valid)
                    {                        
                        ResultObject receivedMessage = extractor.Extract(message, DateTime.UtcNow.AddSeconds(lag), routeProvider.GetFrom() ,false, Image_Support.ImageOutputFormat.bytes);
                        
                        if (receivedMessage.Worked)
                        {
                            if (duplicateControl.IsDuplicate(receivedMessage.HashOfBytes, routeProvider.GetFrom().Group))
                            {
                                result.RecordError(RoutedMessageStatus.duplicate);
                            }
                            else
                            {
                                result.UpdatePayload(embedder.Embed(ref receivedMessage, routeProvider.GetTo(), DateTime.UtcNow, false, Image_Support.ImageOutputFormat.bytes));                           
                            }
                        }
                        else
                        {
                            result.UpdatePayload(receivedMessage);
                        }
                    }
                    else
                    {
                        result.RecordError(RoutedMessageStatus.badRoute);
                    }
                }
                catch (Exception ex)
                {
                    result.RecordException(ex);
                }
                result.DumpToLog("created");
            }

            return result;
        }       
    }
}
