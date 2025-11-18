using Common_Support;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using SixLabors.ImageSharp.PixelFormats;
using System;
using System.Buffers.Text;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace Transport_Support
{
    public class RelayFileProcessor : IFileProcessor
    {
        private const RoutedMessageAction action = RoutedMessageAction.SendImmediate;
        private readonly byte[] fuzz = new byte[24];
        //private readonly IDuplicateManager duplicateControl;
        //private readonly IBanManager banControl;
        private readonly IRouteProvider routeProvider;
        private readonly ITempFileManager tempFileManager;
        //private readonly IEmbedder embedder;
        private readonly ILogger<TransmitterFileProcessor> logger;
        private readonly ILoggerFactory loggerFactory;
        private readonly List<RoutedMessageStatus> goodCodes = new List<RoutedMessageStatus>();
        

        public List<RoutedMessageStatus> GetSuccessList
        {
            get { return goodCodes; }

        }

        public RelayFileProcessor(ILoggerFactory loggerFactoryIn, ILogger<TransmitterFileProcessor> loggerIn, IRouteProvider routeProviderIn, ITempFileManager tempFileManagerIn)
        {
            logger = loggerIn;
            routeProvider = routeProviderIn;
            loggerFactory = loggerFactoryIn;
            tempFileManager = tempFileManagerIn;

            using (logger.BeginScope("Constructing Relay Processor"))
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

                    goodCodes.Add(RoutedMessageStatus.banned);
                    goodCodes.Add(RoutedMessageStatus.duplicate);
                    goodCodes.Add(RoutedMessageStatus.messageSent);

                    new Random((int)(DateTime.UtcNow.Ticks % Int32.MaxValue)).NextBytes(fuzz);

                    logger.LogDebug("Constructed");
                }
                catch (Exception ex)
                {
                    logger.LogCritical(ex, "Encountered Exception");
                }
            }
        }

        public RoutedMessage ProcessOutboundFile(string fileName)
        {
            //file processors create message with fake sender which will be ignored when the client connection sends them later

            string sender = TransportSupport.getRandomSender();

            RoutedMessage result = new RoutedMessage(loggerFactory, routeProvider.GetTo().HubUrl, sender, routeProvider.GetTo().Group, fuzz, fileName,   RoutedMessageAction.SendImmediate);

            using (logger.BeginScope("Sending Relayed File"))
            {
                try
                {
                    if (routeProvider.Valid)
                    {
                        ResultObject payload = tempFileManager.GetBase64(fileName);

                        result.UpdatePayload(payload);                    
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
