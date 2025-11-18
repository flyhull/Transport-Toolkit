using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Transport_Support;
using System.Runtime.CompilerServices;

/// <summary>
/// Reflector - Receive time based encrypted messages interlaced into generated png images from the signalr hub, recover the cyphertext from the images, decrypt it using using time based decryption, re-encrypt it using time based encryption and a different key, interlace the new cyphertext into a generated png image send it back to the signalr hub after a random delay
/// </summary>
/// 

string nodeName = TransportSupport.getMyProgramsNodeNameBeforeBuild();

var builder = Host.CreateApplicationBuilder(args);

builder.Logging.ClearProviders();
builder.Logging.AddSimpleConsole(logOptions =>
{
    logOptions.IncludeScopes = true;
    logOptions.SingleLine = false;
    logOptions.UseUtcTimestamp = true;
    logOptions.TimestampFormat = "HH:mm:ss ";
});

builder.Services.AddMemoryCache();
builder.Services.AddScoped<ILoggerFactory, LoggerFactory>();
builder.Services.AddTransient<IUsage, ReflectorRole>();
builder.Services.AddSingleton<IDuplicateManager, DuplicateControl>();
builder.Services.AddSingleton<IRouteProvider, RouteList>();
builder.Services.AddSingleton<ILocalKey, DummyKey>();
builder.Services.AddSingleton<IMessageTracker, DummyTracker>();
builder.Services.AddTransient<IExtractor, Extractor>();
builder.Services.AddTransient<IEmbedder, Embedder>();
builder.Services.AddSingleton<ITempFileManager, TempFileManager>();
builder.Services.AddTransient<INetworkProcessor,DefaultNetworkProcessor>();

builder.Services.AddHostedService<CacheWatcher>();
builder.Services.AddHostedService<ListeningClient>();

var host = builder.Build();

ILogger logger = host.Services.GetRequiredService<ILogger<Program>>();

using (logger.BeginScope(nodeName + " Startup"))
{
    logger.LogInformation("Program running in " + Environment.CurrentDirectory ?? "an unknown directory");
    logger.LogInformation("Dependencies running in " + TransportSupport.getWorkingDirectory());

    logger.LogInformation("THIS WINDOW WILL SHOW MESSAGES BEING TRANSFORMED AS THEY ARE RETURNED TO A SIGNALR HUB");

    logger.LogInformation(nodeName + " Ready");

    host.Run();
}

using (logger.BeginScope(nodeName + " Shutdown"))
{
    logger.LogInformation(nodeName + " Ran");
}

Console.WriteLine("Press enter to exit");
Console.ReadLine();
