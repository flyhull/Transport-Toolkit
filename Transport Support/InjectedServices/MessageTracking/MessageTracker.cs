using Common_Support;
using Facade_Support;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.VisualBasic;
using NSec.Cryptography;
using System;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;
using System.Linq;
using System.Reflection.Metadata.Ecma335;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Time_Based_Encryption;
using static System.Net.WebRequestMethods;

namespace Transport_Support
{
    public class MessageTracker : IMessageTracker, IDisposable
    {
        public bool Tracking
        {
            get { return true; }
        }

        private readonly string IgnorePrefix = "NOTRACK";
        private readonly TempByteArray _userPassPhrase;
        private readonly TempBytesThatHoldsDateTime _userSecretDate;

        private readonly IMemoryCache cache;
        private readonly int secondsToTrackFor = TransportSupport.getMaximumTransitTimeInSeconds();
        private readonly ILogger<MessageTracker> logger;
        private readonly string module = "receipt";
        private readonly IUsage param;
        private readonly string receivedDirName = "";
        private readonly string outboundDirName = "";
        private readonly bool receivedDirValid = false;
        private readonly bool outboundDirValid = false;

        SpinLock spLock = new SpinLock();

        private bool disposedValue;

        public TempByteArray userPassPhrase
        {
            get { return _userPassPhrase; }
        }

        public TempBytesThatHoldsDateTime userSecretDate
        {
            get { return _userSecretDate; }
        }

        public Int32 EncryptedReceiptMessageLength
        {
            get { return 48; }
        }

        public MessageTracker(ILogger<MessageTracker> loggerIn, IMemoryCache cacheIn, IUsage paramIn)
        {
            cache = cacheIn;
            logger = loggerIn;
            param = paramIn;
           
            using (logger.BeginScope("Constructing Message Tracker"))
            {
                try
                {
                    _userPassPhrase = new TempByteArray(TransportSupport.GetPassphrase(module));
                    _userSecretDate = new TempBytesThatHoldsDateTime(TransportSupport.GetSecretDateTime(module));
                    logger.LogDebug("Constructed");
                }
                catch (Exception ex)
                {
                    _userPassPhrase = new TempByteArray(Array.Empty<byte>());
                    _userSecretDate = new TempBytesThatHoldsDateTime(DateTime.UtcNow);
                    logger.LogCritical(ex, "Encountered Exception");
                }


                try
                {
                    receivedDirName = Path.Combine(Environment.CurrentDirectory, paramIn.OutputSubDirectory);
                    outboundDirName = Path.Combine(Environment.CurrentDirectory, paramIn.WatchedSubDirectory);

                    logger.LogDebug("Will be receiving into directory " + receivedDirName);

                    DirectoryInfo receivedDirectory = new DirectoryInfo(receivedDirName);

                    if (receivedDirectory.Exists)
                    {
                        logger.LogDebug(receivedDirName + " exists");
                    }
                    else
                    {
                        receivedDirectory.Create();
                        logger.LogDebug("Created " + receivedDirName);
                    }

                    if (TransportSupport.DirectoryIsWritable(receivedDirectory))
                    {
                        logger.LogDebug(receivedDirName + " is writable");
                        receivedDirValid = true;
                    }
                    else
                    {
                        logger.LogCritical(receivedDirName + " is unusable");
                    }

                    logger.LogDebug("Will be sending into directory " + outboundDirName);

                    DirectoryInfo outboundDirectory = new DirectoryInfo(outboundDirName);

                    if (outboundDirectory.Exists)
                    {
                        logger.LogDebug(outboundDirName + " exists");
                    }
                    else
                    {
                        outboundDirectory.Create();
                        logger.LogDebug("Created " + outboundDirName);
                    }

                    if (TransportSupport.DirectoryIsWritable(outboundDirectory))
                    {
                        logger.LogDebug(outboundDirName + " is writable");
                        outboundDirValid = true;
                    }
                    else
                    {
                        logger.LogCritical(outboundDirName + " is unusable");
                    }
                }
                catch (Exception ex)
                {
                    logger.LogCritical(ex, "Encountered Exception");
                }



            }
        }
        private string GetTrackingNumber(byte[] contentHash)
        {
            logger.LogWarning("The hash of the original message is " + BitConverter.ToString(contentHash));
            return BitConverter.ToString(contentHash).Replace("-", string.Empty);            
        }

        private static void CacheItemRemoved(object identifier, object? cachedDesciption, EvictionReason evictionReason, object? state)
        {
            try
            {
                if (state == null)
                {
                    Console.WriteLine("ERROR: Message Tracker missing so we cannot log eviction of " + (string)identifier);
                }
                else
                {
                    MessageTracker cameFrom = (MessageTracker)state;

                    using (cameFrom.logger.BeginScope("Logging eviction of " + (string)identifier + " from cache"))
                    {
                        if (cachedDesciption == null)
                        {
                            cameFrom.logger.LogWarning("Description of " + (string)identifier + " did not survive evicted from cache because " + evictionReason.ToString());
                        }
                        else
                        {
                            cameFrom.logger.LogWarning((string)identifier + ((string)cachedDesciption ?? " oops") + " evicted from cache because " + evictionReason.ToString());

                            bool received = evictionReason == EvictionReason.Removed;

                            string? filename = (string?)cachedDesciption;

                            ResultObject notification = cameFrom.WriteFileToReceivedFolder(received, (string)identifier, filename);

                            if (notification.WroteFile)
                            {
                                cameFrom.logger.LogDebug("File written for " + identifier );
                            }
                            else
                            {
                                cameFrom.logger.LogError("File could not be written for " + identifier);
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("ERROR: Encountered " + ex.ToString() + " while logging eviction of " + (string)identifier);
            }
        }

        private ResultObject WriteFileToReceivedFolder(bool received, string identifier, string? filename)
        {
            ResultObject result = new ResultObject();

            string activity = "writing reciept notification file to received folder";

            string message = string.Empty;
            string topic = string.Empty;

            if (received)
            {
                message = (filename ?? "Unknown file") +  " was received";
                topic = "Receipt of ";
            }
            else
            {
                message = (filename ?? "Unknown file") + " should have been delivered by now";
                topic = "Notification about ";
            }
                ;
            using (logger.BeginScope("Indicating that " + message + " for " + identifier))
            {
                try
                {
                    if (receivedDirValid)
                    {
                        logger.LogDebug("Starting to write file");

                        FileInfo fi = new FileInfo( receivedDirName + Path.DirectorySeparatorChar + topic + identifier + DateTime.UtcNow.ToString("u").Replace(":", string.Empty) + ".txt");

                        if (fi.Exists)
                        {
                            result.RecordTransportIssue(TransportIssue.file_could_not_be_written, activity);
                        }
                        else
                        {
                            System.IO.File.WriteAllText(fi.FullName, message);
                            result = new ResultObject(fi, true);
                        }
                    }
                    else
                    {
                        logger.LogError("Receipt Directory Missing");
                        result.RecordTransportIssue(TransportIssue.received_directory_invalid, activity);
                    }
                }
                catch (Exception ex)
                {
                    logger.LogError("Encountered exception");
                    result = new ResultObject(ex, activity);
                }
            }

            return result;
        }

        public ResultObject SendReceiptBack(byte[] contentHash)
        {
            ResultObject result = new ResultObject();

            string activity = "writing receipt acknowledgement file to sending folder";

            using (logger.BeginScope("Reporting that " + BitConverter.ToString(contentHash) + " has arrived"))
            {
                try
                {
                    string identifier = GetTrackingNumber(contentHash);
                    logger.LogInformation("Representing message with " + identifier);
                    DateTime rightNow = DateTime.UtcNow;
                    ValidationSummary validation = new ValidationSummary();

                    logger.LogInformation("Using tracking key and encryption timestamp of " + rightNow.ToString("u"));

                    logger.LogWarning("to encrypt " + BitConverter.ToString(contentHash));

                    ResultObject EncryptReceipt = UseTimeToStatically.Encrypt(contentHash, _userPassPhrase.bytes, _userSecretDate.Timestamp, rightNow, TimeBasedCryptionLimits.MinimumArgon2MemorySize, TimeBasedCryptionLimits.MinimumArgon2NumberOfPasses, validation);

                    if (EncryptReceipt.Worked)
                    { 
                        if (outboundDirValid)
                        {
                            logger.LogDebug("Starting to write file");

                            FileInfo fi = new FileInfo(outboundDirName + Path.DirectorySeparatorChar + IgnorePrefix + identifier + DateTime.UtcNow.ToString("u").Replace(":", string.Empty) + ".bin");

                            if (fi.Exists)
                            {
                                result.RecordTransportIssue(TransportIssue.file_could_not_be_written, activity);
                            }
                            else
                            {
                                System.IO.File.WriteAllBytes(fi.FullName, EncryptReceipt.Bytes);
                                result = new ResultObject(fi, true);
                            }
                        }
                        else
                        {
                            logger.LogError("Outbound Directory Missing");
                            result.RecordTransportIssue(TransportIssue.outbound_directory_invalid, activity);
                        }
                    }
                    else
                    {
                        logger.LogError("Cannot encrypt content hash to make receipt message");
                    }
                }
                catch (Exception ex)
                {
                    logger.LogError("Encountered exception");
                    result = new ResultObject(ex, activity);
                }
            }

            return result;
        }

        public ResultObject Received(byte[] contents)
        {
            //decrypt inbound message and see if the identifier of the hash of the original message is in cache

            // if so write received message file and return result object

            // if not return result object with "not_receipt_message" error

            using (logger.BeginScope("Seeing if " + BitConverter.ToString(MD5.HashData(contents)) + " had been sent before"))
            {
                ResultObject result = new ResultObject();
                string activity = "seeing if message is acknowledging a receipt";
                bool lockTaken = false;

                try
                {
                    if (contents.Length > 0)
                    {
                        DateTime rightNow = DateTime.UtcNow;
                        ValidationSummary validation = new ValidationSummary();

                        logger.LogInformation("Will decrypt using tracking key and guessed encryption timestamp of " + rightNow.AddSeconds(-1 * TransportSupport.getMinimumDelayInSeconds()).ToString("u").Replace(":", string.Empty));

                        ResultObject DecryptReceipt = UseTimeToStatically.Decrypt(contents, _userPassPhrase.bytes, _userSecretDate.Timestamp, rightNow, 2, TransportSupport.getMaximumTransitTimeInSeconds() + TransportSupport.getMaxDecryptionSeconds(), TransportSupport.getMinimumTransitTimeInSeconds(), TimeBasedCryptionLimits.MinimumArgon2MemorySize, TimeBasedCryptionLimits.MinimumArgon2NumberOfPasses, validation);

                        if (DecryptReceipt.Worked)
                        {
                            logger.LogWarning("Decrypted to " + BitConverter.ToString(DecryptReceipt.Bytes));

                            logger.LogInformation("Decrypted " + DecryptReceipt.SizeDesc() + " receipt " + DecryptReceipt.DescribeBytes());
                            
                            string identifier = GetTrackingNumber(DecryptReceipt.Bytes);

                            logger.LogInformation("Representing message with " + identifier);

                            logger.LogDebug("Checking to see if " + identifier + " was previously sent");

                            string? cachedDesciption;

                            spLock.Enter(ref lockTaken);

                            logger.LogDebug("Locked code block");

                            logger.LogWarning("using " + identifier + " to search for cache entry");

                            if (cache.TryGetValue(identifier, out cachedDesciption)) { 

                                logger.LogWarning(identifier + " was sent before as " + (cachedDesciption ?? " unknown file"));

                                cache.Remove(identifier);

                                logger.LogWarning(identifier, " removed from cache");

                                result = DecryptReceipt;

                            }
                            else
                            {
                                logger.LogWarning(identifier + " does not seem to be sent before");
                                //result = WriteFileToReceivedFolder(true, identifier, "Unexpected File");
                                result = new ResultObject("Dummy entry to create result that worked");
                            }
                        }
                        else
                        {
                            logger.LogDebug("This is not a receipt");
                            result.RecordTransportIssue(TransportIssue.not_receipt_message, activity);
                        }
                    }
                    else
                    {
                        logger.LogWarning("No contents to track");
                    }
                }
                catch (Exception ex)
                {
                    logger.LogCritical(ex, "Encountered Exception");
                }
                finally
                {
                    if (lockTaken)
                    {
                        spLock.Exit(true);
                        logger.LogDebug("Released code block");
                    }
                }

                return result;
            }            
        }

        public bool Sent(byte[] contentHash, string filename)
        {
            //add identifier of hash of the original message to cache

            //encrypt hash and write to outbound file
            
            bool result = false;

            using (logger.BeginScope("Sending " + BitConverter.ToString(contentHash) ))
            {
                bool lockTaken = false;

                try
                {
                    if (filename.Contains(Path.DirectorySeparatorChar + IgnorePrefix))
                    {
                        logger.LogInformation("Not tracking this one becaise file name starts with " + IgnorePrefix);
                    }
                    else
                    {
                        if (contentHash.Length > 0)
                        {
                            string identifier = GetTrackingNumber(contentHash);

                            logger.LogInformation("Representing message with " + identifier);

                            logger.LogDebug("Checking to see if " + identifier + " was previously sent");

                            string? cachedDesciption;

                            spLock.Enter(ref lockTaken);

                            logger.LogDebug("Locked code block");

                            if (cache.TryGetValue(identifier, out cachedDesciption))
                            {
                                logger.LogDebug(identifier + (cachedDesciption ?? " oops"));

                                logger.LogDebug(identifier + " was already sent as " + filename);

                                logger.LogWarning(identifier + " is a duplicate, prior cached record will be removed so new one can be created");

                                cache.Remove(identifier);
                                logger.LogDebug(identifier, " removed");
                            }
                            else
                            {
                                logger.LogDebug(identifier + " has not been sent before");
                            }

                            logger.LogWarning(identifier + " being recorded in cache");

                            MemoryCacheEntryOptions cacheEntryOptions = new MemoryCacheEntryOptions()
                            .SetPriority(CacheItemPriority.NeverRemove)
                            .SetSize(1)
                            .SetAbsoluteExpiration(DateTime.UtcNow.AddSeconds(secondsToTrackFor))
                            .RegisterPostEvictionCallback(CacheItemRemoved, this);
                            cache.Set(identifier, filename, cacheEntryOptions);
                            logger.LogDebug(identifier + " recorded as " + filename);

                            result = true;
                        }
                        else
                        {
                        logger.LogWarning("No contens hash to track");
                        }
                    }
                }
                catch (Exception ex)
                {
                    logger.LogCritical(ex, "Encountered Exception");
                }
                finally
                {
                    if (lockTaken)
                    {
                        spLock.Exit(true);
                        logger.LogDebug("Released code block");
                    }
                }
            }

            return result;
        }






        

        //public bool IsDuplicate(byte[] hashOfByteContents, string fromGroup)
        //{
        //    bool result = false;

            

        //    return result;
        //}

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    //fuzz.Redact();
                }

                disposedValue = true;
            }
        }



        void IDisposable.Dispose()
        {
            // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }












    }
}
