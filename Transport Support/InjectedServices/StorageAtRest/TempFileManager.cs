using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Common_Support;
using Facade_Support;
using System.ComponentModel.Design;
using NSec.Cryptography;
using Transport_Support;

namespace Transport_Support
{
    public class TempFileManager : ITempFileManager, IDisposable
    {
        private readonly ILogger<TempFileManager> logger;
        private readonly IUsage role;
        private readonly IEmbedder embedder;
        private readonly IExtractor extractor;

        private DirectoryInfo? tempDirectory;

        private TempByteArray randomKey = new TempByteArray(2048);
        private readonly WayPoint key = new WayPoint();
        private bool disposedValue;
        private string dirName = "";
        public bool tempDirValid = false;

        public TempFileManager(ILogger<TempFileManager> loggerIn, IUsage roleIn, IEmbedder embedderIn, IExtractor extractorIn)
        {
            logger = loggerIn;
            role = roleIn;
            embedder = embedderIn;
            extractor = extractorIn;

            using (logger.BeginScope("Constructing Temp File Manager"))
            {
                try
                {
                    dirName = Path.Combine(Environment.CurrentDirectory, roleIn.TempSubDirectory);

                    logger.LogDebug("Will be managing directory " + dirName);

                    tempDirectory = new DirectoryInfo(dirName);

                    if (tempDirectory.Exists)
                    {
                        logger.LogDebug(dirName + " exists");
                    }
                    else
                    {
                        tempDirectory.Create();
                        logger.LogDebug("Created " + dirName);
                    }

                    if (TransportSupport.DirectoryIsWritable(tempDirectory))
                    {
                        logger.LogDebug(dirName + " is writable");
                        tempDirValid = true;
                    }
                    else
                    {
                        logger.LogCritical(dirName + " is unusable");
                    }

                    key.Group = "None";
                    key.HubUrl = "localhost";
                    key.PassPhrase = CommonSupport.GetRandomString(128, 256);
                    key.SecretDate  = DateTime.UtcNow.ToString("s");
                    key.Pattern = "Random";
                    key.Color = "Random";

                    if (key.Complete)
                    {
                        logger.LogDebug("Key is complete");
                    }
                    else
                    {
                        logger.LogError("Key not generated correctly");
                    }
                }
                catch (Exception ex)
                {
                    logger.LogCritical(ex, "Encountered Exception");
                }
            }
        }
        public ResultObject DeleteFile(string filename)
        {
            ResultObject result = new ResultObject();

            string activity = "deleting temporary file " + filename;

            try
            {
                logger.LogDebug("Trying to delete " + filename);

                if (tempDirValid && (tempDirectory != null))
                {
                    if (string.IsNullOrEmpty(filename))
                    {
                        logger.LogError("Filename Missing");
                        result.RecordTransportIssue(TransportIssue.filename_missing, activity);
                    }
                    else
                    {
                        FileInfo fi = new FileInfo(filename);

                        if (fi.Exists)
                        {
                            if (fi.FullName.StartsWith(tempDirectory.FullName))
                            {
                                logger.LogDebug("Starting to delete file");

                                fi.Delete();
                                fi.Refresh();

                                if (fi.Exists)
                                {
                                    logger.LogError("File not deleted");
                                    result.RecordTransportIssue(TransportIssue.operation_failed, activity);
                                }
                                else
                                {
                                    logger.LogDebug("File successfully deleted");
                                    result = new ResultObject(new FileInfo(filename), false);
                                }
                            }
                            else
                            {
                                logger.LogError("File in wrong directory");
                                result.RecordTransportIssue(TransportIssue.file_not_in_temp_directory, activity);
                            }
                        }
                        else
                        {
                            logger.LogError("File not present");
                            result.RecordTransportIssue(TransportIssue.file_missing, activity);
                        }
                    }
                }
                else
                {
                    logger.LogDebug("");
                }
            }
            catch (Exception ex)
            {
                logger.LogError("Encountered exception " + ex.Message + " while " + activity);
                result = new ResultObject(ex, activity);
            }
            return result;
        }

        public ResultObject GetBase64(string filename)
        {
            ResultObject result = new ResultObject();

            string activity = "retrieving Base64 from temporary file " + filename;

            ValidationSummary validation = new ValidationSummary();

            try
            {
                if (tempDirValid && (tempDirectory != null))
                {
                    if (string.IsNullOrEmpty(filename))
                    {
                        logger.LogError("Filename Missing");
                        result.RecordTransportIssue(TransportIssue.filename_missing, activity);
                    }
                    else
                    {
                        if (filename.StartsWith(tempDirectory.FullName))
                        {
                            logger.LogDebug("Starting to retrieve bytes from file");

                            FileInfo fi = new FileInfo(filename);

                            if (fi.Exists)
                            {
                                result = FacadeSupport.GetBytesFromFile(fi.FullName, validation);

                                if (result.Worked)
                                {                                
                                    result = extractor.Extract(result, fi.LastWriteTimeUtc, key, true, Image_Support.ImageOutputFormat.base64);
                                    if (result.Worked)
                                    {
                                        logger.LogDebug("Extraction from temporary file worked");

                                        DeleteFile(filename);                                            
                                    }
                                    else
                                    {
                                        logger.LogError("Extraction from temporary file failed");
                                    }                                
                                }
                                else
                                {
                                    logger.LogError(string.Join(Environment.NewLine, validation.ListValidationIssues()));
                                    result.RecordTransportIssue(TransportIssue.file_could_not_be_read, activity);
                                }
                            }
                            else
                            {
                                logger.LogError("Temporary file does not exist");
                            }
                        }
                        else
                        {
                            logger.LogError("File in wrong directory");
                            result.RecordTransportIssue(TransportIssue.file_not_in_temp_directory, activity);
                        }
                    }
                }
                else
                {
                    logger.LogError("");
                    result.RecordTransportIssue(TransportIssue.temp_directory_invalid, activity);
                }
            }
            catch (Exception ex)
            {
                logger.LogError("Encountered exception " + ex.Message + " while " + activity);
                result = new ResultObject(ex, activity);
            }
            return result;
        }

        public ResultObject StoreBytes(ResultObject input)
        {
            ResultObject result = new ResultObject();

            string activity = "storing bytes in new temporary file";
            
            try
            {
                if (tempDirValid && (tempDirectory != null))
                {
                    if (input.Bytes.Length == 0)
                    {
                        logger.LogError("Input Missing");
                        result.RecordTransportIssue(TransportIssue.input_missing, activity);
                    }
                    else
                    {
                        logger.LogDebug("Starting to write bytes to file");

                        result = embedder.Embed(ref input, key, DateTime.UtcNow, true, Image_Support.ImageOutputFormat.file, role.TempSubDirectory);

                        if (result.Worked)
                        {
                            logger.LogDebug("Embedding data into temporary file " + result.FileName + " worked");
                        }
                        else
                        {
                            logger.LogError("Embedding data into temporary file failed");
                        }
                    }
                }
                else 
                {
                    logger.LogError("Temp Directory Missing");
                    result.RecordTransportIssue(TransportIssue.temp_directory_invalid,activity);           
                }
            }
            catch (Exception ex)
            {
                logger.LogError("Encountered exception " + ex.Message + " while " + activity);
                result = new ResultObject(ex, activity);
            }
            return result;
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    randomKey.Redact();
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
