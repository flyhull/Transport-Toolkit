using System;
using System.Buffers.Text;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.NetworkInformation;
using System.Text;
using System.Threading.Tasks;
using Common_Support;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using SixLabors.ImageSharp.PixelFormats;
using Time_Based_Encryption;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace Transport_Support
{
    public enum RoutedMessageStatus
    {
        none,
        banned,
        badRoute,
        duplicate,
        wrongColor,
        exception,
        readyToSend,
        wroteBytes,
        wroteFile,
        extracted,
        embedded,
        couldNotExtract,
        couldNotEmbed,
        couldNotReadFile,
        messageSent,
        disConnected,
        fileProcessed,
        unExpected,
        noConnection,
        seePayloadStatus,
        fileMissing,
        fileNameMissing,
        fileEmpty,
        sentButCouldNotStartTracking
    }

    public enum RoutedMessageAction
    {
        DoNothing,
        CacheAndSend,
        SendImmediate,
        ReportError,
        WriteForWatcher
    }
    
    public class RoutedMessage 
    {
        public readonly string group = string.Empty;
        public readonly string messageId = string.Empty;
        public readonly string hubUrl = string.Empty;
        public readonly string sender = string.Empty;
        private RoutedMessageStatus _Status = RoutedMessageStatus.none;
        private RoutedMessageAction _Action;
        private ResultObject _message = new ResultObject();
        private Exception? _Ex = null;
        private DateTime _TimestampLastChanged = DateTime.UtcNow;
        public byte[] contentHash = Array.Empty<byte>();
        public DateTime TimestampLastChanged
        {
            get { return _TimestampLastChanged; }
        }
        public RoutedMessageStatus Status
        {
            get { return _Status; }
        }
        public RoutedMessageAction Action
        {
            get { return _Action; }
        }
        public ResultObject Payload
        {
            get { return _message; }
        }
        public Exception? Ex
        {
            get { return _Ex; }
        }

        

        public string Description
        {
            get { return messageId + " from " + sender + " to " + group + " at " + hubUrl; }
        }

        private ILogger<RoutedMessage> logger;

        public RoutedMessage(ILoggerFactory loggerFactory, string hubUrlIn, string senderIn, string grp, string msg , byte[] fuzz, RoutedMessageAction actionIn) 
        {
            logger = loggerFactory.CreateLogger<RoutedMessage>();

            using (logger.BeginScope("Constructing Routed Message from string input"))
            {
                try
                {
                    messageId = TransportSupport.getBase64MessageIdentifier(msg, fuzz, grp);
                    _Action = actionIn;
                    group = grp;

                    sender = senderIn;
                    hubUrl = hubUrlIn;

                    if (string.IsNullOrEmpty(msg))
                    {
                        UpdatePayload(new ResultObject());
                        logger.LogDebug("Message was empty");
                    }
                    else
                    {
                        UpdatePayload(new ResultObject(msg));
                        logger.LogDebug(TransportSupport.describeMessageString(msg) + " loaded");
                    }

                    UpdatePayload(new ResultObject(msg));

                    DumpToLog("Constructed routed message");
                }
                catch (Exception ex)
                {
                    logger.LogCritical(ex, "Encountered Exception");
                }
            }
        }

        public RoutedMessage(ILoggerFactory loggerFactory, string hubUrlIn, string senderIn, string grp,  byte[] fuzz, string filename, RoutedMessageAction actionIn)
        {
            logger = loggerFactory.CreateLogger<RoutedMessage>();

            using (logger.BeginScope("Constructing Routed Message from file"))
            {
                try
                {
                    logger.LogDebug("Filename is " + filename);
                    messageId = TransportSupport.getBase64MessageIdentifier(filename, fuzz, grp);
                    _Action = actionIn;
                    group = grp;
                   
                    sender = senderIn;
                    hubUrl = hubUrlIn;

                    byte[] fileContents = File.ReadAllBytes(filename);


                    if (fileContents.Length > 0)
                    {
                        UpdatePayload(new ResultObject(fileContents));
                        logger.LogDebug(Payload.DescribeBytes() + " loaded");
                    }
                    else
                    {                        
                        UpdatePayload(new ResultObject());
                        logger.LogDebug("File was empty");
                    }

                    DumpToLog("Constructed routed message");
                }
                catch (Exception ex)
                {
                    logger.LogCritical(ex, "Encountered Exception");
                }
            }
        }

        public void RecordException(Exception ex)
        {
            using (logger.BeginScope("recording exception about routed message " + messageId))
            {
                _Status = RoutedMessageStatus.exception;
                _Ex = ex;
                logger.LogError("Encountered Exception " + ex.ToString());
            }
        }

        public void RecordError(RoutedMessageStatus error)
        {
            using (logger.BeginScope("Recording error " + error.ToString() + " about routed message " + messageId))
            {
                _Status = error;
                logger.LogDebug("Recorded");
            }
        }
        
        public void UpdatePayload(ResultObject result, bool decache = false)
        {
            using (logger.BeginScope("Updating payload of routed message " + messageId))
            {
                _message = result;

                logger.LogDebug(result.Snapshot);

                _Status = RoutedMessageStatus.seePayloadStatus;

                if (result.WroteFile)
                {
                    _Status = RoutedMessageStatus.wroteFile;
}

                if (result.WroteString)
                {
                    _Status = RoutedMessageStatus.readyToSend;
                    if (decache)
                    {
                        _Action = RoutedMessageAction.SendImmediate;
                    }
                }

                if (result.WroteBytes)
                {
                    _Status = RoutedMessageStatus.wroteBytes;
                }

                _TimestampLastChanged = DateTime.UtcNow;

                logger.LogDebug("Status is " + _Status.ToString());
            }
        }

        public string Snapshot
        {
            get { return string.Join(Environment.NewLine, Spill()); }
        }

        public void DumpToLog(string reason)
        {
            using (logger.BeginScope("Dumping Routed Message"))
            {
                logger.LogDebug("Routed Message " + reason);
                foreach (string line in Spill())
                {
                    logger.LogDebug(line);
                }
            }

        }

        public List<string> Spill()
        {
            List<string> litany = new List<string>();
                            
            litany.Add("MessageId " + messageId + " from " +sender + " to group " + group + " at " + hubUrl);
                
            litany.Add("Status is " + _Status.ToString() + " Pending Action is " +  _Action.ToString());   

            if (!(_Ex == null))
            {
                litany.Add("There was an Exception:");
                litany.Add(_Ex.ToString());
            }

            litany.Add("Payload Follows: ");

            foreach (string line in Payload.Spill())
            {
                litany.Add("* " + line);
            } 

            return litany;
        }
    }  
}
