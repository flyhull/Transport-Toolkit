using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using static System.Runtime.InteropServices.JavaScript.JSType;
using Common_Support;
using System.Threading;
using System.Reflection;

namespace Transport_Support
{    
    public class HubBanControl : IHubBanManager , IDisposable
    {
        private ILogger<HubBanControl> logger;
        private IMemoryCache cache;
        private readonly int hoursToRememberFor = 8;
        private TempByteArray fuzz = new TempByteArray(64);
        private IConnectionLookup lookup;

        SpinLock spLock = new SpinLock();

        public bool IBan
        {
            get { return true; }
        }

        private bool disposedValue;

        public HubBanControl(ILogger<HubBanControl> loggerIn, IMemoryCache cacheIn, IConnectionLookup lookupIn)
        {
            cache = cacheIn;
            logger = loggerIn;
            lookup = lookupIn;

            using (logger.BeginScope("Constructing Ban Control"))
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

        private void CacheItemRemoved(object identifier, object? cachedDesciption, EvictionReason evictionReason, object? state)
        {
            try
            {
                if (state == null)
                {
                    Console.WriteLine("ERROR: Ban Control missing so we cannot log eviction of " + (string)identifier);
                }
                else
                {
                    HubBanControl cameFrom = (HubBanControl)state;

                    using (cameFrom.logger.BeginScope("Banned " + (string)identifier + " removed from cache"))
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

        private string GetIdentifier(string item)
        {
            string result = string.Empty;
            using (MemoryStream ms = new MemoryStream())
            {
                ms.Write(fuzz.bytes);
                ms.Write(MD5.HashData(Encoding.UTF8.GetBytes("ban")));
                ms.Write(MD5.HashData(Encoding.UTF8.GetBytes(item)));
                result =  BitConverter.ToString(MD5.HashData(ms.ToArray()));
            }
            return result;
        }
        public bool IsSenderBanned(string sender)
        {
            using (logger.BeginScope("Finding connection id for " + sender))
            {
                string connectionId = lookup.GetConnectionIdBySender(sender);

                if (string.IsNullOrEmpty(connectionId))
                {
                    logger.LogInformation("Not found");
                    return false;
                }
                else
                {
                    logger.LogDebug("Found");
                    return IsIdBanned(connectionId);
                }
            }
        }
        public bool IsIdBanned(string connectionId)
        {
            using (logger.BeginScope("Checking if connection " + connectionId + " is banned"))
            {
                bool lockTaken = false;

                try
                {
                    logger.LogDebug("Checking to see if connection ID " + connectionId + " has been banned recently");

                    string identifier = GetIdentifier(connectionId);

                    logger.LogDebug("Representing connection ID " + connectionId + " with " + identifier);

                    string? cachedDesciption;

                    spLock.Enter(ref lockTaken);
                    logger.LogDebug("Locked code block");

                    if (cache.TryGetValue(identifier, out cachedDesciption))
                    {
                        logger.LogDebug(identifier + (cachedDesciption ?? " oops"));
                        return true;
                    }
                    else
                    {
                        logger.LogDebug(identifier + " is not banned");
                        return false;
                    }
                }
                catch (Exception ex)
                {
                    logger.LogCritical(ex, "Encountered Exception");
                    return false;
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
        }

        public bool IsIpBanned(string ipAddress)
        {
            bool result = false;

            using (logger.BeginScope("Checking if IP address " + (ipAddress ?? "<missing>") + " is banned" ))
            {
                bool lockTaken = false;

                try
                {
                    if (string.IsNullOrEmpty(ipAddress))
                    {
                        logger.LogDebug("Cannot check missing address");
                    }
                    else
                    {
                        logger.LogDebug("Checking for recent ban");

                        string identifier = GetIdentifier(ipAddress);

                        logger.LogDebug("Representing IP address " + ipAddress + " with " + identifier);

                        string? cachedDesciption;

                        spLock.Enter(ref lockTaken);
                        logger.LogDebug("Locked code block");

                        if (cache.TryGetValue(identifier, out cachedDesciption))
                        {
                            logger.LogDebug(identifier + (cachedDesciption ?? " oops"));
                                result = true;
                        }
                        else
                        {
                            logger.LogDebug(identifier + " is not banned");
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

        public bool BanBySender( string sender)
        {
            using (logger.BeginScope("Banning Sender " + sender))
            {
                string connectionId = lookup.GetConnectionIdBySender(sender);
                logger.LogDebug("Requesting ban using his connectionId " + connectionId);
                return BanById(connectionId);
            }
        }

        public bool BanById(string connectionId)
        {
            bool result = false;
            

            using (logger.BeginScope("Banning connection " + connectionId ))
            {
                bool lockTaken = false;

                try
                {
                    logger.LogWarning("Starting to ban");

                    string idIdentifier = GetIdentifier(connectionId);

                    logger.LogDebug("Representing connection ID " + connectionId + " with " + idIdentifier);

                    string ipAddress = lookup.GetIpAddressByConnectionId(connectionId);

                    string ipIdentifier = GetIdentifier(ipAddress);

                    logger.LogDebug("Representing ip address " + ipAddress + " with " + ipIdentifier);

                    bool alreadyBannedByIp = IsIpBanned(ipAddress);
                    bool alreadyBannedById = IsIdBanned(connectionId);

                    spLock.Enter(ref lockTaken);
                    logger.LogDebug("Locked code block");

                    if (alreadyBannedByIp)
                    {
                        logger.LogDebug("Already banned, prior ban will be removed so new one can be created");
                        cache.Remove(ipIdentifier);
                        logger.LogDebug(ipIdentifier, " removed");
                    }

                    DateTime expiration = DateTime.UtcNow.AddHours(hoursToRememberFor);
                    logger.LogDebug("Creating ban for " + ipIdentifier);
                    MemoryCacheEntryOptions cacheEntryOptions = new MemoryCacheEntryOptions()
                    .SetPriority(CacheItemPriority.NeverRemove)
                    .SetSize(1)
                    .SetAbsoluteExpiration(expiration)
                    .RegisterPostEvictionCallback(CacheItemRemoved, this);
                    cache.Set(ipIdentifier, " was banned at " + DateTime.UtcNow.ToString("u"), cacheEntryOptions);
                    logger.LogDebug("Banned until " + expiration.ToString("u"));

                    if (alreadyBannedById)
                    {
                        logger.LogDebug("Was already banned, prior ban will be removed so new one can be created");
                        cache.Remove(idIdentifier);
                        logger.LogDebug("Removed");
                        result = true;
                    }
                    else
                    {
                        logger.LogDebug("This is a new ban");
                    }
                                            
                    cache.Set(idIdentifier, " was banned at " + DateTime.UtcNow.ToString("u"), cacheEntryOptions);
                    logger.LogDebug("Banned until " + expiration.ToString("u"));
                        
                }
                catch (Exception ex)
                {
                    logger.LogCritical(ex, "Encountered Exception");
                    result = false;
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
