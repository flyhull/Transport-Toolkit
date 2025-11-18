using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Common_Support;

namespace Transport_Support
{
    
    public class DuplicateControl : IDuplicateManager, IDisposable
    {
        private ILogger<DuplicateControl> logger; 
        private IMemoryCache cache;
        private readonly int hoursToRememberFor = 1;

        private TempByteArray fuzz = new TempByteArray(1024);

        SpinLock spLock = new SpinLock();
       

        private bool disposedValue;

        public DuplicateControl(ILogger<DuplicateControl> loggerIn, IMemoryCache cacheIn)
        {
            cache = cacheIn;
            logger = loggerIn;

            using (logger.BeginScope("Constructing Duplicate Control"))
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

        private static void CacheItemRemoved(object identifier, object? cachedDesciption, EvictionReason evictionReason, object? state)
        {
            try
            {
                if (state == null)
                {
                    Console.WriteLine("ERROR: Duplicate Control missing so we cannot log eviction of " + (string)identifier);
                }
                else
                {
                    DuplicateControl cameFrom = (DuplicateControl)state;

                    using (cameFrom.logger.BeginScope("Logging eviction of " + (string)identifier + " from cache"))
                    {
                        if (cachedDesciption == null)
                        {
                            cameFrom.logger.LogDebug("Description of " + (string)identifier + " did not survive evicted from cache because " + evictionReason.ToString());
                        }
                        else
                        {
                            cameFrom.logger.LogDebug((string)identifier + ((string)cachedDesciption ?? " oops") + " evicted from cache because " + evictionReason.ToString());
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("ERROR: Encountered " + ex.ToString() + " while logging eviction of " + (string)identifier);
            }
        }

        private string GetMessageSignature(byte[] hash, string fromGroup )
        {
            string result = string.Empty;
            using (MemoryStream ms = new MemoryStream())
            {
                ms.Write(fuzz.bytes);
                ms.Write(hash);
                ms.Write(MD5.HashData(Encoding.UTF8.GetBytes(fromGroup)));
                result = BitConverter.ToString(MD5.HashData(ms.ToArray())).Replace("-", string.Empty);
            }
            return result;
        }

        public bool IsDuplicate(byte[] hashOfByteContents, string fromGroup)
        {
            bool result = false;

            using (logger.BeginScope("Seeing if " + BitConverter.ToString(hashOfByteContents) + " had come from " + fromGroup))
            {
                bool lockTaken = false;

                try
                {
                    string identifier = GetMessageSignature(hashOfByteContents, fromGroup);

                    logger.LogInformation("Representing message with " + identifier);

                    logger.LogDebug("Checking to see if " + identifier + " has been seen before");

                    string? cachedDesciption;

                    spLock.Enter(ref lockTaken);

                    logger.LogDebug("Locked code block");

                    if (cache.TryGetValue(identifier, out cachedDesciption))
                    {
                        logger.LogDebug(identifier + (cachedDesciption ?? " oops"));

                        logger.LogDebug(identifier + " was already seen, prior observation will be removed so new one can be created");
                        
                        logger.LogWarning(identifier + " is a duplicate");
                        
                        cache.Remove(identifier);
                        logger.LogDebug(identifier, " removed");
                        result = true;
                    }
                    else
                    {
                        logger.LogDebug(identifier + " has not been seen before");
                    }

                    logger.LogDebug(identifier + " being recorded");

                    MemoryCacheEntryOptions cacheEntryOptions = new MemoryCacheEntryOptions()
                    .SetPriority(CacheItemPriority.NeverRemove)
                    .SetSize(1)
                    .SetAbsoluteExpiration(DateTime.UtcNow.AddHours(hoursToRememberFor))
                    .RegisterPostEvictionCallback(CacheItemRemoved, this);
                    cache.Set(identifier, " was last seen " + DateTime.UtcNow.ToString("u"), cacheEntryOptions);
                    logger.LogDebug(identifier + " recorded");      
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

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    fuzz.Redact();
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
