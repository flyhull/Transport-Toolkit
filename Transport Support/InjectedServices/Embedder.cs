// Ignore Spelling: Encrypter

using Common_Support;
using Facade_Support;
using Microsoft.Extensions.Logging;
using Obfuscation_in_Generated_PNG;
using System;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using Time_Based_Encryption;

namespace Transport_Support
{
    public class Embedder : IEmbedder, IDisposable
    {
        private readonly ILogger<Embedder> logger;
        private readonly ILoggerFactory loggerFactory;
        private readonly ILocalKey localKey;
        private TimeStampObject outboundSecretTs = new TimeStampObject(new DateTime());
        private TempByteArray encryptPassphrase = new TempByteArray(1);
        private TempBytesThatHoldsDateTime encryptTimestamp = new TempBytesThatHoldsDateTime(new DateTime());
        private String prompts = string.Empty;

        private bool disposedValue;

        public Embedder(ILogger<Embedder> loggerIn, ILoggerFactory loggerFactoryIn, ILocalKey LocalKeyIn)
        {
            logger = loggerIn;
            loggerFactory = loggerFactoryIn;
            localKey = LocalKeyIn;

            using (logger.BeginScope("Constructing Embedder"))
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

        public ResultObject Embed(ref ResultObject inputToEncrypt, WayPoint destination, DateTime encryptionTime, bool forTemp, Image_Support.ImageOutputFormat format, string subDirectoryName = "Error")
        {   
            outboundSecretTs = new TimeStampObject(destination.SecretDate);
            encryptPassphrase = new TempByteArray(Encoding.UTF8.GetBytes(destination.PassPhrase));
            encryptTimestamp = new TempBytesThatHoldsDateTime(outboundSecretTs.TimeStampValue);
            prompts = destination.Prompts;
            ResultObject result = new ResultObject();
            ResultObject bytesToEncrypt;
            ValidationSummary validation;
            string inputType = string.Empty;
            string intermediateType = string.Empty;
            string outputType = string.Empty;

            if (forTemp)
            {
                inputType = "new png obsfucating transit-encryption of message";
                intermediateType = "temp-encryption of new png obsfucating transit-encryption of message";
                outputType = "new png obsfucating temp-encryption of new png obsfucating transit-encryption of message";
            }
            else
            {
                inputType = "[user-encrypted] message";
                intermediateType = "transit-encryption of message";
                outputType = "new png obsfucating transit-encryption of message";
            }

            if (inputToEncrypt.Worked && inputToEncrypt.WroteString)
            {
                logger.LogDebug("Input is in base64 format and will be converted to bytes");
                validation = new ValidationSummary();
                bytesToEncrypt = FacadeSupport.GetBytesFromBase64(inputToEncrypt.Base64String, validation);
                logger.LogInformation("Received " + inputType + " " + bytesToEncrypt.DescribeBytes() + " to send to group " + destination.Group);

            }
            else
            {
                bytesToEncrypt = inputToEncrypt;
                logger.LogInformation("Received " + inputType + " " + bytesToEncrypt.DescribeBytes() + " to send to group " + destination.Group);

                logger.LogDebug("Input hopefully does not need conversion");
            }

            if (bytesToEncrypt.Worked)
            {
                if (bytesToEncrypt.WroteBytes)
                {
                    logger.LogDebug("Input is in byte format as required");
                    validation = new ValidationSummary();
                    ResultObject CreateEncryptedPayload = new ResultObject();

                    if (localKey.IntermediateNode)
                    {
                        

                        logger.LogInformation("Using key for " + destination.Group + " and encryption timestamp of " + encryptionTime.ToString("u"));
                        
                        CreateEncryptedPayload = UseTimeToStatically.Encrypt(bytesToEncrypt.Bytes, encryptPassphrase.bytes, encryptTimestamp.Timestamp, encryptionTime, TimeBasedCryptionLimits.MinimumArgon2MemorySize, TimeBasedCryptionLimits.MinimumArgon2NumberOfPasses, validation);

                        logger.LogInformation("Created " + intermediateType + " " + CreateEncryptedPayload.DescribeBytes());
                        

                    }
                    else // endpoint
                    {
                        
                                               
                        // add local key layer here

                        logger.LogInformation("Using key for sender and encryption timestamp of " + encryptionTime.ToString("u"));

                        ResultObject PreEncryptedPayload = UseTimeToStatically.Encrypt(bytesToEncrypt.Bytes, localKey.UserPassPhrase.bytes, localKey.UserSecretDate.Timestamp, encryptionTime, TimeBasedCryptionLimits.MinimumArgon2MemorySize, TimeBasedCryptionLimits.MinimumArgon2NumberOfPasses, validation);

                        if (PreEncryptedPayload.Worked)
                        {
                            validation = new ValidationSummary();

                            logger.LogInformation("Using key for " + destination.Group + " and encryption timestamp of " + encryptionTime.ToString("u"));

                            CreateEncryptedPayload = UseTimeToStatically.Encrypt(PreEncryptedPayload.Bytes, encryptPassphrase.bytes, encryptTimestamp.Timestamp, encryptionTime, TimeBasedCryptionLimits.MinimumArgon2MemorySize, TimeBasedCryptionLimits.MinimumArgon2NumberOfPasses, validation);

                           
                        }
                        else
                        {
                            logger.LogError("Could not perform user encryption");
                            result = PreEncryptedPayload;
                        }

                    }

                    //write result to file if successful

                    if (CreateEncryptedPayload.Worked && CreateEncryptedPayload.WroteBytes)
                    {
                        logger.LogInformation("Created " + intermediateType + " " + CreateEncryptedPayload.DescribeBytes());

                        validation = new ValidationSummary();

                        if (format == Image_Support.ImageOutputFormat.file)
                        {
                            string existingOutputDirectoryName = Path.Combine(Environment.CurrentDirectory, subDirectoryName);

                            result = ImageGenerator.CreateRgba32PngByInterlacingEncryptedBytes(CreateEncryptedPayload.Bytes, destination.Prompts, format, existingOutputDirectoryName, validation);

                            logger.LogInformation("Wrote a new " + result.SizeDesc() + " " + outputType + " to  file " + result.FileName);
                        }
                        else
                        {
                            result = ImageGenerator.CreateRgba32PngByInterlacingEncryptedBytes(CreateEncryptedPayload.Bytes, destination.Prompts, format, "", validation);

                            logger.LogInformation("Created a " + result.SizeDesc() + " " + outputType + " " + CreateEncryptedPayload.DescribeBytes());
                        }

                        CreateEncryptedPayload.Redact();
                    }
                    else
                    {
                        logger.LogError("Could not perform transport encryption");
                        result = CreateEncryptedPayload;
                    }


                }
                else
                {
                    logger.LogError("Input is not in byte format");
                    result.RecordTransportIssue(TransportIssue.input_invalid, "validating format");
                }
            }
            else
            {
                logger.LogError("Input is misformatted");
                result.RecordTransportIssue(TransportIssue.input_invalid, "validating input");
            }

            bytesToEncrypt.Redact();

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
            outboundSecretTs = new TimeStampObject(new DateTime());
            prompts = string.Empty;
            encryptPassphrase.Redact();
            encryptTimestamp.Redact();
        }
    }
}
