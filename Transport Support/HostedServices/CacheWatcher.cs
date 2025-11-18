using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;
using Microsoft.Extensions.Caching.Memory;
using MimeDetective.Storage;
using System.Diagnostics;
using System.Reflection;

namespace Transport_Support
{
    //public class xCacheWatcher : BackgroundService 
    //{
    //    private ILogger<CacheWatcher> logger;
    //    private IMemoryCache cache;
    //    private readonly IConfiguration configuration;

    //    private readonly string myName = "";

    //    private bool found = false;
    //    //private object? dummy;

    //    public CacheWatcher(IConfiguration configurationIn, ILogger<CacheWatcher> loggerIn, IMemoryCache cacheIn)
    //    {
    //        logger = loggerIn;
    //        cache = cacheIn;
    //        configuration = configurationIn;

    //        using (logger.BeginScope("Constructing Cache Watcher"))
    //        {
    //            try
    //            {
    //                myName = configuration["MyName"] ?? "unknown";
    //                Console.Title = myName;

    //                logger.LogDebug("Constructing Cache Watcher for " + myName + " in " + Environment.CurrentDirectory);
    //            }
    //            catch (Exception ex)
    //            {
    //                logger.LogCritical(ex, "Encountered Exception");
    //            }
    //        }

    //    }
    //    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    //    {
    //        using (logger.BeginScope("Running " + myName+ " Cache Watcher"))
    //        {
    //            try
    //            {
    //                logger.LogDebug("Starting");

    //                while (!stoppingToken.IsCancellationRequested)
    //                {
    //                    found = cache.TryGetValue("NotHash", out _);
    //                    //logger.LogDebug("Checked Cache");
    //                    await Task.Delay(1000,stoppingToken);
    //                }

    //                logger.LogDebug("Stopped normally");

    //            }
    //            catch (Exception ex)
    //            {
    //                logger.LogCritical(ex, "Stopped due to Exception");
    //                base.Dispose();
    //            }

    //            return ;
    //        }
    //    }
    //}


    public class CacheWatcher : IHostedService
    {
        private Timer? _timer;
        private ILogger<CacheWatcher> logger;
        private IMemoryCache cache;
        private readonly IConfiguration configuration;

        private readonly string myName = "";

        private bool found = false;

        public CacheWatcher(IConfiguration configurationIn, ILogger<CacheWatcher> loggerIn, IMemoryCache cacheIn)
        {
            logger = loggerIn;
            cache = cacheIn;
            configuration = configurationIn;

            List<string> entries = GetAllKeys(cache);

            using (logger.BeginScope("Constructing Cache Watcher"))
            {
                logger.LogDebug("Constructing Cache Watcher");

                try
                {
                    myName = configuration["MyName"] ?? "unknown";
                    Console.Title = myName;

                    foreach (string itemKey in entries)
                    {
                        cache.Remove(itemKey);
                    }

                    entries.Clear();

                    logger.LogInformation("Cache cleared");

                    logger.LogDebug("Constructed Cache Watcher for " + myName + " in " + Environment.CurrentDirectory);
                }
                catch (Exception ex)
                {
                    logger.LogCritical(ex, "Encountered Exception");
                }
            }
        }

        public static List<string> GetAllKeys(IMemoryCache memoryCache)
        {
            var coherentState = typeof(MemoryCache).GetField("_coherentState", BindingFlags.NonPublic | BindingFlags.Instance);
            if (coherentState == null)
            {
                return new List<string>(); // Or handle error appropriately
            }

            var coherentStateValue = coherentState.GetValue(memoryCache);
            if (coherentStateValue == null)
            {
                return new List<string>();
            }

            var entriesCollection = coherentStateValue.GetType().GetProperty("EntriesCollection", BindingFlags.NonPublic | BindingFlags.Instance);
            if (entriesCollection == null)
            {
                return new List<string>();
            }

            var entriesCollectionValue = entriesCollection.GetValue(coherentStateValue) as dynamic;
            if (entriesCollectionValue == null)
            {
                return new List<string>();
            }

            var keys = new List<string>();
            foreach (var item in entriesCollectionValue)
            {
                var keyProperty = item.GetType().GetProperty("Key");
                if (keyProperty != null)
                {
                    var val = keyProperty.GetValue(item);
                    if (val != null)
                    {
                        keys.Add(val.ToString());
                    }
                }
            }
            return keys;
        }

        public Task StartAsync(CancellationToken cancellingToken)
        {
            using (logger.BeginScope("Starting " + myName + " Cache Watcher"))
            {
                logger.LogDebug("Starting");

                try
                {                    
                    _timer = new Timer(ActionToBePerformed, null, TimeSpan.Zero, TimeSpan.FromSeconds(1));
                    logger.LogInformation("Started");

                }
                catch (Exception ex)
                {
                    logger.LogCritical(ex, "Failed to start due to Exception");
                }
            }

            return Task.CompletedTask;
        }

        void ActionToBePerformed(object? state)
        {
            found = cache.TryGetValue("NotHash", out _);
            //logger.LogDebug("Checked Cache");
        }

        public Task StopAsync(CancellationToken cancellingToken)
        {
            using (logger.BeginScope("Stopping " + myName + " Cache Watcher"))
            {
                logger.LogDebug("Stopping");

                try
                {
                    _timer?.Change(Timeout.Infinite, 0);
                    logger.LogDebug("Stopped normally");
                }
                catch (Exception ex)
                {
                    logger.LogCritical(ex, "Failed to stop due to Exception");
                }
            }
            
            return Task.CompletedTask;
        }
    }



}
