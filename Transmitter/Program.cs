using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Transport_Support;
using System.Reflection;

/// <summary>
/// Transmitter - Encrypt image files in a 'to send' folder using time based encryption, interlace them into a generated png image and send them to the reflector via the signalr hub
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
builder.Services.AddTransient<IUsage, TransmitterRole>();
builder.Services.AddSingleton<IRouteProvider, EndPoint>();
builder.Services.AddSingleton<ILocalKey, LocalKey>();
builder.Services.AddSingleton<IMessageTracker, DummyTracker>();
//builder.Services.AddTransient<IExtractor, Extractor>();
builder.Services.AddTransient<IEmbedder, Embedder>();
builder.Services.AddSingleton<ITempFileManager, DummyFileManager>();
builder.Services.AddTransient<IFileProcessor, TransmitterFileProcessor>();
builder.Services.AddTransient<INetworkProcessor, DummyNetworkProcessor>();

builder.Services.AddHostedService<FileWatcher>();

var host = builder.Build();

ILogger logger = host.Services.GetRequiredService<ILogger<Program>>();

using (logger.BeginScope(nodeName + " Startup"))
{
    logger.LogInformation("Program running in " + Environment.CurrentDirectory ?? "an unknown directory");
    logger.LogInformation("Dependencies running in " + TransportSupport.getWorkingDirectory());

    logger.LogInformation("THIS WINDOW WILL SHOW MESSAGE FILES BEING DISCOVERED IN A FOLDER AND THEN SENT TO A SIGNALR HUB");

    logger.LogInformation(nodeName + " Ready");

    host.Run();
}

using (logger.BeginScope(nodeName + " Shutdown"))
{
    logger.LogInformation(nodeName + " Ran");
}

Console.WriteLine("Press enter to exit");
Console.ReadLine();