using Hubs;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Configuration;
using Microsoft.Extensions.Logging.Console;
using Microsoft.Extensions.Options;
using System.Text.Json;
using Transport_Support;

/// <summary>
/// Hub - Receive time based encrypted messages interlaced into generated png images from signalr clients, recover the cyphertext from the images, decrypt it using using time based decryption, re-encrypt it using time based encryption and a different key, interlace the new cyphertext into a generated png image send it to different signalr clients after a random delay
/// </summary>

string nodeName = TransportSupport.getMyProgramsNodeNameBeforeBuild();

var builder = WebApplication.CreateBuilder();

builder.Logging.ClearProviders();
builder.Logging.AddSimpleConsole(logOptions =>
{
    logOptions.IncludeScopes = true;
    logOptions.SingleLine = false;
    logOptions.UseUtcTimestamp = true;
    logOptions.TimestampFormat = "HH:mm:ss ";
});

builder.Services.AddMemoryCache();
builder.Services.AddSingleton<ILoggerFactory, LoggerFactory>();
builder.Services.AddTransient<IUsage,HubRole>();
builder.Services.AddSingleton<IDuplicateManager, DuplicateControl>();
builder.Services.AddSingleton<IConnectionLookup, ConnectionLookup>();
builder.Services.AddSingleton<IHubBanManager, HubBanControl>();
builder.Services.AddSingleton<IRouteProvider, RouteList>();
builder.Services.AddSingleton<ILocalKey, DummyKey>();
builder.Services.AddSingleton<IMessageTracker, DummyTracker>();
builder.Services.AddTransient<IExtractor, Extractor>();
builder.Services.AddTransient<IEmbedder, Embedder>();
builder.Services.AddTransient<IHubProcessor, CachingHubProcessor>();
builder.Services.AddSingleton<ITempFileManager, TempFileManager>();

builder.Services.AddRazorPages();

builder.Services.AddSignalR(    
    hubOptions => 
    {
        hubOptions.EnableDetailedErrors = true;
        hubOptions.KeepAliveInterval = TimeSpan.FromSeconds(TransportSupport.getKeepAliveSeconds());
        hubOptions.ClientTimeoutInterval = TimeSpan.FromSeconds(2 * TransportSupport.getKeepAliveSeconds());
        hubOptions.MaximumReceiveMessageSize = 1024 * 48;
    }
    )
    .AddJsonProtocol(
    options =>
    {
        options.PayloadSerializerOptions.PropertyNamingPolicy = null;
    }
    )
    ;

// Change to use Name as the user identifier for SignalR
// WARNING: This requires that the source of your JWT token 
// ensures that the Name claim is unique!
// If the Name claim isn't unique, users could receive messages 
// intended for a different user!

builder.Services.AddSingleton<IUserIdProvider, UserNameProvider>();

// Change to use email as the user identifier for SignalR
// builder.Services.AddSingleton<IUserIdProvider, EmailBasedUserIdProvider>();

// WARNING: use *either* the NameUserIdProvider *or* the 
// EmailBasedUserIdProvider, but do not use both. 

builder.Services.AddHostedService<CacheWatcher>();

var app = builder.Build();

IConfiguration config = app.Services.GetRequiredService<IConfiguration>();

Console.WriteLine("This should be the same node name: " + TransportSupport.getMyProgramsNodeNameAfterBuild(config));

ILogger logger = app.Services.GetRequiredService<ILogger<Program>>();

using (logger.BeginScope(nodeName + " Startup"))
{
    // Configure the HTTP request pipeline.
    if (!app.Environment.IsDevelopment())
    {
        app.UseExceptionHandler("/Error");
        // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
        app.UseHsts();
    }

    //app.UseStaticFiles();

    app.UseRouting();

    app.UseAuthorization();

    app.MapRazorPages();

    app.MapHub<ChatHub>("/chatHub", options =>
    {
        options.AllowStatefulReconnects = false;
    });

    logger.LogInformation("Program running in " + Environment.CurrentDirectory ?? "an unknown directory");
    logger.LogInformation("Dependencies running in " + TransportSupport.getWorkingDirectory());

    logger.LogInformation("THIS WINDOW WILL SHOW MESSAGES BEING TRANSFORMED AS THEY GO THROUGH A SIGNALR HUB");

    logger.LogInformation(nodeName + " Ready");

    app.Run();
}

using (logger.BeginScope(nodeName + " Shutdown"))
{
    logger.LogInformation(nodeName + " Ran");
}

Console.WriteLine("Press enter to exit");
Console.ReadLine();