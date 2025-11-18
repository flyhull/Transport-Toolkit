using Common_Support;
using Microsoft.Extensions.Logging;
using MimeDetective.Storage;
using System;
using System.Buffers.Text;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace Transport_Support
{
    public class TransmitterFileProcessor : IFileProcessor
    {
        private const RoutedMessageAction action = RoutedMessageAction.SendImmediate;
		private readonly byte[] fuzz = new byte[24];
		//private readonly IDuplicateManager duplicateControl;
		//private readonly IBanManager banControl;
		private readonly IRouteProvider routeProvider;
        //private readonly ITempFileManager tempFileManager;
        private readonly IEmbedder embedder; 
        private readonly ILogger<TransmitterFileProcessor> logger;
        private readonly ILoggerFactory loggerFactory;
        private readonly List<RoutedMessageStatus> goodCodes = new List<RoutedMessageStatus>();
        private readonly IUsage myParam;



        public List<RoutedMessageStatus> GetSuccessList
        {
            get { return goodCodes; }
        }

        //builder.Services.AddSingleton<IRouteProvider, EndPoint>();
        //builder.Services.AddTransient<IEmbedder, Embedder>();
        //builder.Services.AddTransient<IOutboundFileProcessor, TransmitterProcessor>();
        public TransmitterFileProcessor(ILoggerFactory loggerFactoryIn, ILogger<TransmitterFileProcessor> loggerIn, IRouteProvider routeProviderIn, IEmbedder embedderIn, IUsage paramIn)
        {
            logger = loggerIn;
            routeProvider = routeProviderIn;
            loggerFactory = loggerFactoryIn;
            embedder = embedderIn;
            myParam = paramIn;

            using (logger.BeginScope("Constructing Transmitter Processor"))
            {
                try
                {
                    goodCodes.Add(RoutedMessageStatus.messageSent);

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
       
        

        /// <summary>
        /// Used to send a Base64 image which contains an encrypted message via Signalr by 
        /// encrypting and embedding a new message in an image and sending it to a group as Base64
        ///  1) get file name
        ///  2) read bytes
        ///  3) embed bytes in image
        ///  4) get image as base64
        ///  5) send base64 to group
        /// </summary>
        /// <param name="fileName">Full name of the file including the path</param>
        /// <returns>RoutedMessage</returns>
        //RoutedMessage ProcessOutboundFile(string fileName);
        public RoutedMessage ProcessOutboundFile(string fileName)
        {           
            //file processors create message with fake sender which will be ignored when the client connection sends them later

            string sender = TransportSupport.getRandomSender();

            RoutedMessage result = new RoutedMessage(loggerFactory, routeProvider.GetTo().HubUrl, sender, routeProvider.GetTo().Group, fuzz, fileName, action);

            using (logger.BeginScope("Sending New File"))
            {
                try
                {
                    if (routeProvider.Valid)
                {
                    
                        byte[] bytesToSend = File.ReadAllBytes(fileName);

                        ResultObject input = new ResultObject(bytesToSend);

                        result.contentHash = input.HashOfBytes;
                        
                        DateTime predatedEncryptionTime = DateTime.UtcNow.AddSeconds(-1 * TransportSupport.getMinimumDelayInSeconds());

                        if (input.Worked)
                        {
                            //if (myParam.ProgramPurpose == Purpose.Client)
                            //{

                            //    ResultObject payload = embedder.Embed(ref input, routeProvider.GetTo(), predatedEncryptionTime, false, Image_Support.ImageOutputFormat.base64);
                            //    result.UpdatePayload(payload);
                            //}
                            //else
                            //{
                                ResultObject payload = embedder.Embed(ref input, routeProvider.GetTo(), predatedEncryptionTime, false, Image_Support.ImageOutputFormat.base64);
                                result.UpdatePayload(payload);
                            //}

                            logger.LogDebug("Processed message");
                        }
                        else
                        {
                            result.RecordError(RoutedMessageStatus.couldNotReadFile);
                        }
                    
                }
                else
                {
                    result.RecordError(RoutedMessageStatus.badRoute);
                }
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Encountered Exception");
                    result.RecordException(ex);
                }
                result.DumpToLog("created");
            }
            return result;
        }
                
        
    }

}
