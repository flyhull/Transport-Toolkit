// Ignore Spelling: Decrypter

using Common_Support;
using Facade_Support;
using Microsoft.Extensions.Logging;
using Microsoft.VisualBasic;
using Obfuscation_in_Generated_PNG;
using SixLabors.Fonts.Unicode;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Time_Based_Encryption;

namespace Transport_Support
{
    public class Extractor : IExtractor, IDisposable
    {
        private readonly ILogger<Extractor> logger;
        private readonly ILoggerFactory loggerFactory;
        private readonly ILocalKey localKey;
        private readonly IMessageTracker messageTracker;

        private TimeStampObject inboundSecretTs = new TimeStampObject(new DateTime());
        private TempByteArray decryptPassphrase = new TempByteArray(1);
        private TempBytesThatHoldsDateTime decryptTimestamp = new TempBytesThatHoldsDateTime(new DateTime());

        private bool disposedValue;

        public Extractor(ILogger<Extractor> loggerIn, ILoggerFactory loggerFactoryIn, ILocalKey LocalKeyIn, IMessageTracker messageTrackerIn)
        {
            logger = loggerIn;
            loggerFactory = loggerFactoryIn;
            localKey = LocalKeyIn;
            messageTracker = messageTrackerIn;

            using (logger.BeginScope("Constructing Extractor"))
            {
                try
                {                    
                    logger.LogDebug("Constructed");
                }
                catch (Exception ex)
                {
                    logger.LogCritical(ex, "Encountered Exception");
                }
            }
        }

        public ResultObject Extract(string input, DateTime encryptionTime, WayPoint origin, bool forTemp, Image_Support.ImageOutputFormat format, string subDirectoryName = "Error")
        {
            string inputType = string.Empty;

            if (forTemp)
            {
                inputType = "new png obsfucating temp-encryption of new png obsfucating transit-encryption of message";
            }
            else
            {
                inputType = "new png obsfucating transit-encryption of message";
            }

            ValidationSummary validation = new ValidationSummary();
            ResultObject GetInterlacedImage = FacadeSupport.GetBytesFromBase64(input, validation);
            logger.LogInformation("Retrieved " + GetInterlacedImage.DescribeBytes() + " " + inputType + " from " + TransportSupport.describeMessageString(input) + " sent to group " + origin.Group);
            return Extract(GetInterlacedImage, encryptionTime, forTemp, format, origin, subDirectoryName);
        }
        public ResultObject Extract(ResultObject input, DateTime timestamp, WayPoint origin, bool forTemp, Image_Support.ImageOutputFormat format, string subDirectoryName = "Error")
        {
            string inputType = string.Empty;

            if (forTemp)
            {
                inputType = "new png obsfucating temp-encryption of new png obsfucating transit-encryption of message";
            }
            else
            {
                inputType = "new png obsfucating transit-encryption of message";
            }

            logger.LogInformation("Received " + input.DescribeBytes() + " " + inputType + " sent to " + origin.Group);
            return Extract(input, timestamp, forTemp, format, origin, subDirectoryName);
        }

        private ResultObject WriteReceivedMessageToFile (ref ValidationSummary validation, ref ResultObject DecryptPayloadImage, string outputType, string subDirectoryName)
        {
            ResultObject result = new ResultObject();
            
            string existingOutputDirectoryName = Path.Combine(Environment.CurrentDirectory, subDirectoryName);
            validation = new ValidationSummary();
            result = FacadeSupport.WriteFileFromBytes(ref DecryptPayloadImage, existingOutputDirectoryName, "", "", validation, false);
            logger.LogInformation("Wrote " + outputType + " " + DecryptPayloadImage.DescribeBytes() + " to  file " + result.FileName);

            if (messageTracker.Tracking)
            {
                logger.LogWarning("Acknowledging receipt of message with a has of " + BitConverter.ToString(DecryptPayloadImage.HashOfBytes));
                result = messageTracker.SendReceiptBack(DecryptPayloadImage.HashOfBytes);
                
            }
            else
            {
                logger.LogDebug("No need to write receipt");
            }

            return result;
        }
        private ResultObject Extract(ResultObject GetInterlacedImage, DateTime timestamp, bool forTemp, Image_Support.ImageOutputFormat format, WayPoint origin, string subDirectoryName)
        {
            inboundSecretTs = new TimeStampObject(origin.SecretDate);
            decryptPassphrase = new TempByteArray(Encoding.UTF8.GetBytes(origin.PassPhrase));
            decryptTimestamp = new TempBytesThatHoldsDateTime(inboundSecretTs.TimeStampValue);
            ValidationSummary validation = new ValidationSummary();
            ResultObject result = new ResultObject();
            string intermediateType = string.Empty;
            string outputType = string.Empty;

            if (forTemp)
            {
                intermediateType = "temp-encryption of new png obsfucating transit-encryption of message";
                outputType = "new png obsfucating transit-encryption of message";
            }
            else
            {
                intermediateType = "transit-encryption of message";
                outputType = "[user-encrypted] message";
            }

            if (GetInterlacedImage.Worked)
            {
                if (GetInterlacedImage.WroteBytes)
                {                
                    ResultObject DeinterlaceCyphertext = ImageGenerator.GetInterlacedEncryptedBytesFromRgba32PngBytes(GetInterlacedImage.Bytes, validation);

                    logger.LogInformation("Extracted " + DeinterlaceCyphertext.SizeDesc() + " " + intermediateType + " " + DeinterlaceCyphertext.DescribeBytes());

                    GetInterlacedImage.Redact();


                    if (DeinterlaceCyphertext.Worked)
                    {

                        logger.LogInformation("Will decrypt using key for group " + origin.Group + " and guessed encryption timestamp of " + timestamp.ToString("u"));

                        ResultObject DecryptPayloadImage = UseTimeToStatically.Decrypt(DeinterlaceCyphertext.Bytes, decryptPassphrase.bytes, decryptTimestamp.Timestamp, timestamp, 2, TransportSupport.getMaximumLookbackTimeInSeconds(), 1, TimeBasedCryptionLimits.MinimumArgon2MemorySize, TimeBasedCryptionLimits.MinimumArgon2NumberOfPasses, validation);

                        DeinterlaceCyphertext.Redact();

                        if (DecryptPayloadImage.Worked)
                        {
                            logger.LogInformation("Decrypted " + DecryptPayloadImage.SizeDesc() + " " + outputType + " " + DecryptPayloadImage.DescribeBytes());

                            if (localKey.IntermediateNode)
                            {
                                switch (format)
                                {
                                    case Image_Support.ImageOutputFormat.base64:
                                        validation = new ValidationSummary();
                                        result = FacadeSupport.GetBase64FromBytes(DecryptPayloadImage.Bytes, validation);
                                        logger.LogInformation("Converted " + outputType + " " + DecryptPayloadImage.DescribeBytes() + " to Base64 " + TransportSupport.describeMessageString(result.Base64String));
                                        break;
                                    case Image_Support.ImageOutputFormat.file:
                                        string existingOutputDirectoryName = Path.Combine(Environment.CurrentDirectory, subDirectoryName);
                                        validation = new ValidationSummary();


                                        result = FacadeSupport.WriteFileFromBytes(ref DecryptPayloadImage, existingOutputDirectoryName, "", "", validation, false);
                                        logger.LogInformation("Wrote " + outputType + " " + DecryptPayloadImage.DescribeBytes() + " to  file " + result.FileName);


                                        DecryptPayloadImage.Redact();
                                        break;
                                    default:
                                        result = DecryptPayloadImage;
                                        break;
                                }


                            }
                            else // endpoint
                            {
                                // we know that this is meant for a recipient on this hub because it decrypted

                                DateTime rightNow = DateTime.UtcNow;
                                validation = new ValidationSummary();
                                ResultObject TrackingResult = new ResultObject();

                                logger.LogInformation("Will decrypt using key for sender and guessed encryption timestamp of " + rightNow.AddSeconds(-1 * TransportSupport.getMinimumDelayInSeconds()).ToString("u").Replace(":", string.Empty));

                                ResultObject FullyDecryptedPayload = UseTimeToStatically.Decrypt(DecryptPayloadImage.Bytes, localKey.UserPassPhrase.bytes, localKey.UserSecretDate.Timestamp, rightNow, 2, TransportSupport.getMaximumTransitTimeInSeconds(), TransportSupport.getMinimumTransitTimeInSeconds(), TimeBasedCryptionLimits.MinimumArgon2MemorySize, TimeBasedCryptionLimits.MinimumArgon2NumberOfPasses, validation);

                                DecryptPayloadImage.Redact();
                               
                                if (FullyDecryptedPayload.Worked)
                                {       
                                    // now we know that this is meant for this recipient

                                    switch (format)
                                    {
                                        case Image_Support.ImageOutputFormat.base64:
                                            validation = new ValidationSummary();
                                            result = FacadeSupport.GetBase64FromBytes(FullyDecryptedPayload.Bytes, validation);
                                            logger.LogInformation("Converted " + outputType + " " + FullyDecryptedPayload.DescribeBytes() + " to Base64 " + TransportSupport.describeMessageString(result.Base64String));
                                            break;
                                        case Image_Support.ImageOutputFormat.file:

                                            logger.LogWarning("Message length is " + FullyDecryptedPayload.Bytes.Length.ToString() + " bytes");
                                            
                                            if (messageTracker.Tracking && FullyDecryptedPayload.Bytes.Length == messageTracker.EncryptedReceiptMessageLength)
                                            {
                                                TrackingResult = messageTracker.Received(FullyDecryptedPayload.Bytes);

                                                if (TrackingResult.Worked)
                                                {
                                                    result = TrackingResult;
                                                    logger.LogInformation("This was a delivery receipt message");
                                                    logger.LogInformation("Wrote " + outputType + " " + " receipt message to file " + result.FileName);
                                                }
                                                else
                                                {
                                                    if (TrackingResult.TransportBasedIssue == TransportIssue.not_receipt_message)
                                                    {
                                                        result = WriteReceivedMessageToFile(ref validation, ref FullyDecryptedPayload, outputType, subDirectoryName);
                                                    }
                                                    else
                                                    {
                                                        logger.LogError("There was an error processing the delivery receipt message");
                                                        result = TrackingResult;
                                                    }
                                                }
                                            }
                                            else
                                            {
                                                result = WriteReceivedMessageToFile(ref validation, ref FullyDecryptedPayload, outputType, subDirectoryName);
                                            }

                                            FullyDecryptedPayload.Redact();

                                            break;
                                        default:
                                            result = FullyDecryptedPayload;
                                            break;
                                    }
                                }
                                else
                                {
                                    // here we pretend that the message was for us unless it was a receipt

                                    if (messageTracker.Tracking && DecryptPayloadImage.Bytes.Length == messageTracker.EncryptedReceiptMessageLength)
                                    {
                                        logger.LogInformation("Message is a receipt for someone else");
                                    }
                                    else
                                    {
                                        // write receipt for message that was not sent (which will be ignored)
                                        TrackingResult = messageTracker.Received(DecryptPayloadImage.Bytes);

                                        if (TrackingResult.Worked)
                                        {
                                            logger.LogInformation("Sent receipt as if it was for us");
                                        }
                                        else
                                        {
                                            logger.LogInformation("Failed to send receipt as if it was for us");
                                        }
                                    }

                                    result = FullyDecryptedPayload;

                                    logger.LogInformation("Message is for someone else on this hub");
                                }
                            }

                        }
                        else
                        {

                            logger.LogInformation("Failed to decrypt message which is not for us");

                            result = DecryptPayloadImage;

                            if (messageTracker.Tracking)
                            {
                                logger.LogWarning("Acknowledging receipt of message which was not meant for us");
                                result = messageTracker.SendReceiptBack(new TempByteArray(16).bytes);

                            }
                            else
                            {
                                logger.LogDebug("No need to write receipt");
                            }
                        }
                    }
                    else
                    {
                        result = DeinterlaceCyphertext;
                        logger.LogError("Could not extract cyphertext from image");
                    }
                }
                else
                {
                    logger.LogError("Input is in the wrong format");
                }
            }
            else
            {
                result = GetInterlacedImage;
            }

            return result;
        }


        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    Redact();
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

        public void Redact()
        {
            inboundSecretTs = new TimeStampObject(new DateTime());
            decryptPassphrase.Redact();
            decryptTimestamp.Redact();
        }

    }

}
