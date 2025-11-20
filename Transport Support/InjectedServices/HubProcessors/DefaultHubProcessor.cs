using Common_Support;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static System.Collections.Specialized.BitVector32;

namespace Transport_Support
{
    public class DefaultHubProcessor : IHubProcessor
    {
        private readonly RoutedMessageAction action = RoutedMessageAction.SendImmediate;
        private readonly byte[] fuzz = new byte[24];
        private readonly IDuplicateManager duplicateControl;
        private readonly IConnectionLookup connectionLookup;
        private readonly ILogger<DefaultHubProcessor> logger;
        private readonly ILoggerFactory loggerFactory;
        

        public DefaultHubProcessor(ILoggerFactory loggerFactoryIn, ILogger<DefaultHubProcessor> loggerIn, IDuplicateManager duplicateControlIn, IConnectionLookup connectionLookupIn)
        {
            duplicateControl = duplicateControlIn;
            connectionLookup = connectionLookupIn;
            loggerFactory = loggerFactoryIn;
            logger = loggerIn;

            using (logger.BeginScope("Constructing Dummy Hub Processor"))
            {
                try
                {      
                    new Random((int)(DateTime.UtcNow.Ticks % Int32.MaxValue)).NextBytes(fuzz);

                    logger.LogDebug("Constructed");
                }
                catch (Exception ex)
                {
                    logger.LogCritical(ex, "Encountered Exception");
                }
            }
        }

        public RoutedMessage ProcessMessage( string input, string connectionId, string group , string url)
        {
            //hub processors create message with sender which may be ignored when the hub connection sends them later

            string sender = connectionLookup.GetSenderByConnectionId(connectionId);

            logger.LogDebug("Received message " + TransportSupport.describeMessageString(input) + " to " + group + " from " + sender + " on connection " + connectionId);

            RoutedMessage result = new RoutedMessage(loggerFactory, url, sender, group, input, fuzz, action);

            using (logger.BeginScope("Processing Message " + result.Description))
            {
                try
                {
                    logger.LogDebug("Processed");
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
