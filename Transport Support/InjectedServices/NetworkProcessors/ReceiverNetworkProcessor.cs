using Common_Support;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using SixLabors.Fonts.Tables.AdvancedTypographic;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Transport_Support
{
    public class ReceiverNetworkProcessor : INetworkProcessor
    {
        private readonly byte[] fuzz = new byte[24];
        private readonly IDuplicateManager duplicateControl;
        private readonly IRouteProvider routeProvider;
        private readonly IExtractor extractor;
        private readonly ILogger<ReceiverNetworkProcessor> logger;
        private readonly ILoggerFactory loggerFactory;
        private readonly Int32 lag = -1 * TransportSupport.getMinimumDelayInSeconds();
        private readonly IUsage myParam;
        

        public ReceiverNetworkProcessor(ILoggerFactory loggerFactoryIn, ILogger<ReceiverNetworkProcessor> loggerIn, IDuplicateManager duplicateControlIn, IRouteProvider routeProviderIn, IExtractor extractorIn, IUsage paramIn)
        {
            duplicateControl = duplicateControlIn;
            logger = loggerIn;
            loggerFactory = loggerFactoryIn;
            routeProvider = routeProviderIn;
            myParam = paramIn;
            extractor = extractorIn;

            using (logger.BeginScope("Constructing Receiver Processor"))
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

            RoutedMessage result = new RoutedMessage(loggerFactory, "Local Host", sender, routeProvider.GetTo().Group, message, fuzz, RoutedMessageAction.DoNothing);

            using (logger.BeginScope("Receiving Message on Client"))
            {
                try
                {
                    if (routeProvider.Valid)
                    {
                        result.UpdatePayload(extractor.Extract(message, DateTime.UtcNow.AddSeconds(lag), routeProvider.GetFrom(), false, Image_Support.ImageOutputFormat.file, myParam.OutputSubDirectory));
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
