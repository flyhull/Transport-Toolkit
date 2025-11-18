using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileSystemGlobbing;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace Transport_Support
{
    public class FileDescriptor
    {
        public DateTime lastModified;
        public string fullName;
        public string name;
        public Int32 size;

        public FileDescriptor(FileInfo fi)
        {
            name = fi.Name;
            fullName = fi.FullName;
            lastModified = fi.LastWriteTimeUtc;
            size = GetBytes().Length;
        }

        public byte[] GetBytes()
        {
            return File.ReadAllBytes(fullName);
        }

        public bool Equals(FileDescriptor other)
        {
            return (fullName.Equals(other.fullName) &&
                size.Equals(other.size) &&
                lastModified.Equals(other.lastModified));
        }

    }

    public class FileWatcher : BackgroundService
    {
        private IRouteProvider Routing;
        private IFileProcessor fileProcessor;
        private INetworkProcessor networkProcessor;
        private readonly ILogger<FileWatcher> logger;
        private readonly IConfiguration configuration;
        private readonly ITempFileManager tempFileManager;
        private readonly IClientBanManager clientBanManager;
        private IMemoryCache cache;
        private readonly ILoggerFactory loggerFactory;

        private readonly IMessageTracker messageTracker;

        private readonly string myName = "";
        private readonly string dirName = "";
        private readonly Purpose myRole;

        ClientConnection? connection;
        int patience = 20;
        int impatience = -20;

        private DirectoryInfo watchedDirectory;
        private Dictionary<string, FileDescriptor> priorFiles = new Dictionary<string, FileDescriptor>();
        private Dictionary<string, FileDescriptor> currentFiles = new Dictionary<string, FileDescriptor>();

        private void LoadCurrentDictionary()
        {
            currentFiles = new Dictionary<string, FileDescriptor>();

            foreach (FileInfo fi in watchedDirectory.GetFiles())
            {
                currentFiles.Add(fi.Name, new FileDescriptor(fi));
            }
        }

        private void CheckDirectory(DirectoryInfo di)
        {
            if (di.Exists)
            {
                logger.LogDebug(di.FullName + " exists");
            }
            else
            {
                di.Create();
                logger.LogDebug("Created " + di.FullName);
            }
        }

        public FileWatcher(ILoggerFactory loggerFactoryIn, IUsage roleIn, IConfiguration configurationIn, ITempFileManager tempFileManagerIn, IRouteProvider routes, ILogger<FileWatcher> loggerIn, IFileProcessor fileProcessorIn, INetworkProcessor networkProcessorIn, IMemoryCache cacheIn, IMessageTracker messageTrackerIn, IClientBanManager clientBanManagerIn)
        {
            Routing = routes;
            logger = loggerIn;
            fileProcessor = fileProcessorIn;
            networkProcessor = networkProcessorIn;
            configuration = configurationIn;
            tempFileManager = tempFileManagerIn;
            myRole = roleIn.ProgramPurpose;
            cache = cacheIn;
            loggerFactory = loggerFactoryIn;
            messageTracker = messageTrackerIn;
            clientBanManager = clientBanManagerIn;
            //connectionLogger = connectionLoggerIn;

            using (logger.BeginScope("Constructing File Watcher"))
            {
                try
                {
                    myName = configuration["MyName"] ?? "unknown";
                    Console.Title = myName;

                    logger.LogDebug("Constructing File Watcher for " + myName + " in " + Environment.CurrentDirectory);

                    dirName = Path.Combine(Environment.CurrentDirectory, roleIn.WatchedSubDirectory);

                    logger.LogDebug("Will be watching directory " + dirName);

                    watchedDirectory = new DirectoryInfo(dirName);

                    CheckDirectory(watchedDirectory);

                    if (roleIn.ProgramPurpose == Purpose.Client)
                    {
                        string incomingDirName = Path.Combine(Environment.CurrentDirectory, roleIn.OutputSubDirectory);
                        logger.LogDebug("Will be writting to directory " + incomingDirName);
                        CheckDirectory(new DirectoryInfo(incomingDirName));
                    }
                }
                catch (Exception ex)
                {
                    logger.LogCritical(ex, "Encountered Exception");
                    watchedDirectory = new DirectoryInfo(Environment.CurrentDirectory);
                }
            }
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            using (logger.BeginScope("Starting " + myName + "  File Watcher"))
            {
                try
                {
                    logger.LogDebug("Starting");

                    if (TransportSupport.DirectoryIsWritable(watchedDirectory))
                    {
                        if (Routing.Valid)
                        {
                            bool listen = (myRole != Purpose.Transmitter && myRole != Purpose.Relay);

                            connection = new ClientConnection(loggerFactory, listen, Routing.GetFrom().Group, Routing.GetTo().HubUrl,tempFileManager, cache, fileProcessor, networkProcessor, messageTracker, clientBanManager);

                            logger.LogDebug("Waiting to Connect");

                            while (!connection.IsConnected() && patience > 0)
                            {
                                Task.Delay(10000, stoppingToken).Wait(stoppingToken);
                                patience--;
                            }

                            if (connection.IsConnected())
                            {
                                logger.LogInformation("Started");

                                try
                                {
                                    logger.LogDebug("Initializing Watcher");

                                    LoadCurrentDictionary();

                                    foreach (KeyValuePair<string, FileDescriptor> item in currentFiles)
                                    {
                                        if (!stoppingToken.IsCancellationRequested)
                                        {
                                            priorFiles.Add(item.Key, item.Value);
                                        }
                                        else
                                        {
                                            logger.LogDebug("Cancellation Requested at start");
                                            break;
                                        }
                                    }

                                    logger.LogDebug("Waiting to Watch");

                                    Task.Delay(1000, stoppingToken).Wait(stoppingToken);

                                    logger.LogDebug("Watching");

                                    while (!stoppingToken.IsCancellationRequested)
                                    {

                                        LoadCurrentDictionary();

                                        foreach (FileDescriptor fd in priorFiles.Values)
                                        {
                                            if (stoppingToken.IsCancellationRequested)
                                            {
                                                logger.LogDebug("Cancellation Requested in middle");
                                                break;
                                            }
                                            else
                                            {
                                                if (currentFiles.ContainsKey(fd.name))
                                                {
                                                    if (fd.Equals(currentFiles[fd.name]))
                                                    {
                                                        logger.LogDebug("Watched file " + fd.fullName + " has arrived");

                                                        RoutedMessageStatus sendingResult = connection.ProcessFile(fd.fullName);

                                                        if (fileProcessor.GetSuccessList.Contains<RoutedMessageStatus>(sendingResult))
                                                        {
                                                            File.Delete(fd.fullName);
                                                            logger.LogDebug("Processing for " + fd.fullName + " worked and file was deleted");
                                                        }
                                                        else
                                                        {
                                                            logger.LogError("Sending for " + fd.fullName + " failed with " + sendingResult.ToString());
                                                            //TEMPORARY
                                                            File.Delete(fd.fullName);

                                                        }
                                                    }
                                                    else
                                                    {
                                                        logger.LogDebug("Watched file " + fd.fullName + " is still arriving");
                                                    }
                                                }
                                                else
                                                {
                                                    logger.LogWarning("Watched file " + fd.fullName + " disappeared");
                                                }

                                                if (stoppingToken.IsCancellationRequested)
                                                {
                                                    logger.LogDebug("Cancellation Requested at end");
                                                    break;
                                                }
                                            }
                                        }

                                        if (!stoppingToken.IsCancellationRequested)
                                        {
                                            priorFiles = currentFiles;

                                            if (myRole == Purpose.Relay) //wait for file watcher
                                            {
                                                Task.Delay(TransportSupport.getRandomDelayInSeconds() * 1000, stoppingToken).Wait(stoppingToken);
                                            }
                                            else
                                            {
                                                Task.Delay(1000, stoppingToken).Wait(stoppingToken);
                                            }
                                        }
                                        else
                                        {
                                            logger.LogDebug("Cancellation has been Requested");
                                        }
                                    }

                                    try
                                    {
                                        logger.LogDebug("Stopping as requested");

                                        connection.Disconnect();

                                        while (connection.IsConnected() && impatience < 0)
                                        {
                                            Task.Delay(1000, CancellationToken.None).Wait(CancellationToken.None);
                                            impatience++;
                                        }

                                        if (connection.IsConnected())
                                        {
                                            logger.LogCritical("Did not Stop Quickly Enough");
                                        }
                                        else
                                        {
                                            logger.LogDebug("Stopped as Requested");
                                        }
                                    }
                                    catch (Exception ex)
                                    {
                                        logger.LogCritical(ex, "Did not Stop properly due to Exception");
                                    }
                                }
                                catch (Exception ex)
                                {
                                    logger.LogCritical(ex, "Stopped running due to Exception");
                                    base.Dispose();
                                }
                            }
                            else
                            {
                                logger.LogError("Failed to Start Quickly Enough");
                                base.Dispose();
                            }

                        }
                        else
                        {
                            await Task.Delay(0);
                            logger.LogError("Did not start because route is invalid");
                            base.Dispose();
                        }
                    }
                    else
                    {
                        logger.LogCritical("Did not start because " + watchedDirectory.FullName + " cannot be watched");
                    }
                }
                catch (Exception ex)
                {
                    logger.LogCritical(ex, "Did not start due to Exception");
                    base.Dispose();
                }
            }

            return;
        }
    }

    //public class FileWatcher : IHostedService
    //{
    //    private Timer? _timer ;
    //    private IRouteProvider Routing;
    //    private IFileProcessor fileProcessor;
    //    private INetworkProcessor networkProcessor;
    //    private readonly ILogger<FileWatcher> logger;
    //    private readonly IConfiguration configuration;
    //    private readonly ITempFileManager tempFileManager;
    //    private IMemoryCache cache;
    //    private readonly ILoggerFactory loggerFactory;

    //    private readonly string myName = "";
    //    private readonly string dirName = "";
    //    private readonly Purpose myRole;

    //    ClientConnection? connection;
    //    int patience = 20;
    //    int impatience = -20;

    //    private DirectoryInfo watchedDirectory;
    //    private Dictionary<string, FileDescriptor> priorFiles = new Dictionary<string, FileDescriptor>();
    //    private Dictionary<string, FileDescriptor> currentFiles = new Dictionary<string, FileDescriptor>();
    //    private CancellationToken cancellationRequest;
    //    private bool active = false;

    //    private void LoadCurrentDictionary()
    //    {
    //        currentFiles = new Dictionary<string, FileDescriptor>();

    //        foreach (FileInfo fi in watchedDirectory.GetFiles())
    //        {
    //            currentFiles.Add(fi.Name, new FileDescriptor(fi));
    //        }
    //    }

    //    public FileWatcher(ILoggerFactory loggerFactoryIn, IUsage roleIn, IConfiguration configurationIn, ITempFileManager tempFileManagerIn, IRouteProvider routes, ILogger<FileWatcher> loggerIn, IFileProcessor fileProcessorIn, IMemoryCache cacheIn)
    //    {
    //        Routing = routes;
    //        logger = loggerIn;
    //        fileProcessor = fileProcessorIn;
    //        networkProcessor = new DummyNetworkProcessor();
    //        configuration = configurationIn;
    //        tempFileManager = tempFileManagerIn;
    //        myRole = roleIn.ProgramPurpose;
    //        cache = cacheIn;
    //        loggerFactory = loggerFactoryIn;

    //        using (logger.BeginScope("Constructing File Watcher"))
    //        {
    //            logger.LogDebug("Constructing File Watcher");

    //            try
    //            {
    //                myName = configuration["MyName"] ?? "unknown";
    //                Console.Title = myName;

    //                logger.LogDebug("Constructing File Watcher for " + myName + " in " + Environment.CurrentDirectory);

    //                dirName = Path.Combine(Environment.CurrentDirectory, roleIn.WatchedSubDirectory);

    //                logger.LogDebug("Will be watching directory " + dirName);

    //                watchedDirectory = new DirectoryInfo(dirName);

    //                if (watchedDirectory.Exists)
    //                {
    //                    logger.LogDebug(dirName + " exists");
    //                }
    //                else
    //                {
    //                    watchedDirectory.Create();
    //                    logger.LogDebug("Created " + dirName);
    //                }

    //                logger.LogDebug("Constructed");
    //            }
    //            catch (Exception ex)
    //            {
    //                logger.LogCritical(ex, "Encountered Exception");
    //                watchedDirectory = new DirectoryInfo(Environment.CurrentDirectory);
    //            }
    //        }
    //    }

    //    public Task StartAsync(CancellationToken cancellingToken)
    //    {
    //    using (logger.BeginScope("Starting " + myName + " File Watcher"))
    //    {
    //        logger.LogDebug("Starting");
    //        try
    //        {
    //            cancellationRequest = cancellingToken;

    //            if (TransportSupport.DirectoryIsWritable(watchedDirectory))
    //            {
    //                if (Routing.Valid)
    //                {
    //                    bool listen = (myRole != Purpose.Transmitter && myRole != Purpose.Relay);

    //                    connection = new ClientConnection(loggerFactory, listen, Routing.GetTo(), tempFileManager, cache, fileProcessor, networkProcessor);

    //                    logger.LogDebug("Waiting to Connect");

    //                    while (!connection.IsConnected() && patience > 0)
    //                    {
    //                        Task.Delay(10000, cancellingToken).Wait(cancellingToken);
    //                        patience--;
    //                    }

    //                    if (connection.IsConnected())
    //                    {
    //                        logger.LogDebug("Started");


    //                        logger.LogDebug("Initializing Watcher");

    //                        LoadCurrentDictionary();

    //                        foreach (KeyValuePair<string, FileDescriptor> item in currentFiles)
    //                        {
    //                            if (!cancellingToken.IsCancellationRequested)
    //                            {
    //                                priorFiles.Add(item.Key, item.Value);
    //                            }
    //                            else
    //                            {
    //                                logger.LogDebug("Cancellation Requested at start");
    //                                break;
    //                            }
    //                        }

    //                        logger.LogDebug("Waiting to Watch");

    //                        Task.Delay(1000, cancellingToken).Wait(cancellingToken);

    //                        _timer = new Timer(ActionToBePerformed, null, TimeSpan.Zero, TimeSpan.FromSeconds(1));

    //                        logger.LogDebug("Watching");


    //                        logger.LogDebug("Started");

    //                    }
    //                    else
    //                    {
    //                        logger.LogError("Failed to Start Quickly Enough");
    //                    }

    //                }
    //                else
    //                {
    //                    logger.LogError("Did not start because route is invalid");
    //                }
    //            }
    //            else
    //            {
    //                logger.LogCritical("Did not start because " + watchedDirectory.FullName + " cannot be watched");
    //            }


    //        }
    //        catch (Exception ex)
    //        {
    //            logger.LogCritical(ex, "Failed to start due to Exception");
    //        }
    //    }

    //    return Task.CompletedTask;
    //}

    //void ActionToBePerformed( object? state)
    //{
    //        if (connection == null)
    //        {
    //            logger.LogError("Failing to process as there is no connection");
    //        }
    //        else
    //        {
    //            if (active)
    //            {
    //                logger.LogError("Skipping processing since prior is still running");
    //            }
    //            else
    //            {
    //                logger.LogError("Processing");

    //                active = true;

    //                LoadCurrentDictionary();

    //                foreach (FileDescriptor fd in priorFiles.Values)
    //                {
    //                    if (cancellationRequest.IsCancellationRequested)
    //                    {
    //                        logger.LogDebug("Cancellation Requested in middle");
    //                        break;
    //                    }
    //                    else
    //                    {
    //                        if (currentFiles.ContainsKey(fd.name))
    //                        {
    //                            if (fd.Equals(currentFiles[fd.name]))
    //                            {
    //                                logger.LogDebug("Watched file " + fd.fullName + " has arrived");

    //                                RoutedMessageStatus sendingResult = connection.ProcessFile(fd.fullName);

    //                                if (fileProcessor.GetSuccessList.Contains<RoutedMessageStatus>(sendingResult))
    //                                {
    //                                    File.Delete(fd.fullName);
    //                                    logger.LogDebug("Processing for " + fd.fullName + " worked and file was deleted");
    //                                }
    //                                else
    //                                {
    //                                    logger.LogError("Sending for " + fd.fullName + " failed with " + sendingResult.ToString());
    //                                }
    //                            }
    //                            else
    //                            {
    //                                logger.LogDebug("Watched file " + fd.fullName + " is still arriving");
    //                            }
    //                        }
    //                        else
    //                        {
    //                            logger.LogWarning("Watched file " + fd.fullName + " disappeared");
    //                        }

    //                        if (cancellationRequest.IsCancellationRequested)
    //                        {
    //                            logger.LogDebug("Cancellation Requested at end");
    //                            break;
    //                        }
    //                    }
    //                }

    //                if (cancellationRequest.IsCancellationRequested)
    //                {
    //                    logger.LogDebug("Cancellation has been Requested");                        
    //                }
    //                else
    //                {
    //                    priorFiles = currentFiles;

    //                    if (myRole == Purpose.Relay) //delay processing
    //                    {
    //                        Task.Delay(TransportSupport.getRandomDelayInSeconds() * 1000, cancellationRequest).Wait(cancellationRequest);
    //                    }
    //                    else
    //                    {
    //                        Task.Delay(1000, cancellationRequest).Wait(cancellationRequest);
    //                    }
    //                    active = false;
    //                    logger.LogDebug("Cycle completed");
    //                }
    //            }
    //        }
    //    }
    //    public Task StopAsync(CancellationToken cancellingToken)
    //    {
    //        using (logger.BeginScope("Stopping " + myName + " File Watcher"))
    //        {
    //            logger.LogDebug("Stopping");

    //            try
    //            {
    //                cancellationRequest = cancellingToken;

    //                _timer?.Change(Timeout.Infinite, 0);

    //                logger.LogDebug("Stopping as requested");

    //                if (connection == null)
    //                {
    //                    logger.LogCritical("Connection does not need to be stopped");
    //                }
    //                else
    //                {
    //                    logger.LogCritical("Connection Stopping");

    //                    connection.Disconnect();

    //                    while (connection.IsConnected() && impatience < 0)
    //                    {
    //                        Task.Delay(1000, cancellingToken).Wait(cancellingToken);
    //                        impatience++;
    //                    }

    //                    if (connection.IsConnected())
    //                    {
    //                        logger.LogCritical("Connection did not Stop Quickly Enough");
    //                    }
    //                    else
    //                    {
    //                        logger.LogDebug("Connection stopped as Requested");
    //                    }

    //                }
    //            }
    //            catch (Exception ex)
    //            {
    //                logger.LogCritical(ex, "Failed to stop due to Exception");
    //            }
    //        }

    //        return Task.CompletedTask;
    //    }
    //}
}
