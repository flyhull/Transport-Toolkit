using GalleryTwo.Hubs;
using Microsoft.AspNetCore.SignalR;
using Utilities;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorPages();
builder.Services.AddSignalR(hubOptions =>
{
    hubOptions.EnableDetailedErrors = true;
    hubOptions.KeepAliveInterval = TimeSpan.FromMinutes(1);
});


// Change to use Name as the user identifier for SignalR
// WARNING: This requires that the source of your JWT token 
// ensures that the Name claim is unique!
// If the Name claim isn't unique, users could receive messages 
// intended for a different user!
builder.Services.AddSingleton<IUserIdProvider, GalleryTwo.UserNameProvider>();

// Change to use email as the user identifier for SignalR
// builder.Services.AddSingleton<IUserIdProvider, EmailBasedUserIdProvider>();

// WARNING: use *either* the NameUserIdProvider *or* the 
// EmailBasedUserIdProvider, but do not use both. 



var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}
app.UseStaticFiles();

app.UseRouting();

app.UseAuthorization();

app.MapRazorPages();

app.MapHub<ChatHub>("/chatHub", options =>
{
    options.AllowStatefulReconnects = true;
});

app.Run();
