using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
//using Microsoft.AspNetCore.SignalR.Client;
//using Microsoft.Extensions.Caching.Memory;
//using Microsoft.Extensions.Configuration;
//using Microsoft.Extensions.FileSystemGlobbing;
//using Microsoft.Extensions.Logging;
using Transport_Support;
;

/// <summary>
/// Receiver - Receive png cover images from the reflector via the signalr hub, recover the cyphertext from the images, decrypt it using using time based decryption and save the result as a file in a 'received' folder for sorting
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
builder.Services.AddTransient<IUsage, ReceiverRole>();
builder.Services.AddSingleton<IDuplicateManager, DuplicateControl>();
builder.Services.AddSingleton<IRouteProvider, EndPoint>();
builder.Services.AddSingleton<ILocalKey, LocalKey>();
builder.Services.AddSingleton<IMessageTracker, DummyTracker>();
//builder.Services.AddTransient<IEmbedder, Embedder>();
builder.Services.AddTransient<IExtractor, Extractor>();
builder.Services.AddSingleton<ITempFileManager, DummyFileManager>();
builder.Services.AddTransient<INetworkProcessor, ReceiverNetworkProcessor>();

builder.Services.AddHostedService<ListeningClient>();

var host = builder.Build();

ILogger logger = host.Services.GetRequiredService<ILogger<Program>>();

using (logger.BeginScope(nodeName + " Startup"))
{
    logger.LogInformation("Program running in " + Environment.CurrentDirectory ?? "an unknown directory");
    logger.LogInformation("Dependencies running in " + TransportSupport.getWorkingDirectory());

    logger.LogInformation("THIS WINDOW WILL SHOW MESSAGES BEING RECEIVED FROM A SIGNALR HUB AND BEING WRITTEN TO A FOLDER AS FILES");

    logger.LogInformation(nodeName + " Ready");

    host.Run();
}

using (logger.BeginScope(nodeName + " Shutdown"))
{
    logger.LogInformation(nodeName + " Ran");

}

Console.WriteLine("Press enter to exit");
Console.ReadLine();




