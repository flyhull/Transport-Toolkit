using Microsoft.AspNetCore.Mvc.Formatters;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Caching.Memory;
using System;
using System.Timers;

namespace GalleryTwo.Hubs
{
    public class ChatHub : Hub
    {
        private readonly string? myName = "";
        private IMemoryCache cache;
        private System.Threading.Timer aTimer;

        public ChatHub() {
            IConfigurationBuilder cbuilder = new ConfigurationBuilder().AddJsonFile("appsettings.json", false, true);
            IConfigurationRoot croot = cbuilder.Build();
            myName = croot["MyName"];
            Console.WriteLine("Starting " + myName);
            cache = new MemoryCache(new MemoryCacheOptions());
            aTimer = new System.Threading.Timer(new System.Threading.TimerCallback(OnTimedEvent), cache, 0, 1000);            
        }
             

        private static void OnTimedEvent(Object? source)
        {
            if (source == null)
            {
                Console.WriteLine("Cache is Missing");
            }
            else
            {
                Console.WriteLine("Cache Was Flushed");
                IMemoryCache cache = (IMemoryCache)source;
                cache.Get("NotHash");
            }
        }

        public async Task SendMessage(string user, string message)
        {
            int expirationSeconds = new Random(DateTime.Now.Millisecond).Next(5, 15);


            MemoryCacheEntryOptions cacheEntryOptions = new MemoryCacheEntryOptions()
                .SetPriority(CacheItemPriority.NeverRemove)
                .SetSize(1)
                .SetAbsoluteExpiration(DateTime.UtcNow.AddSeconds(expirationSeconds))
                .RegisterPostEvictionCallback(CacheItemRemoved, this);
            cache.Set(message, user, cacheEntryOptions);
            Console.WriteLine("Delayed  at " + DateTime.UtcNow.ToLongTimeString());

            await Clients.All.SendAsync("ReceiveMessage", myName, message);

        }


        private static void CacheItemRemoved(
            object message, object? user, EvictionReason evictionReason, object? state)
        {

            Console.WriteLine("Attempting Resend");

            if (state == null)
            {
                Console.WriteLine("Hub Missing");
            }
            else
            {

                ChatHub cameFrom = (ChatHub)state;

                object userOfRecord = "Unknown";
                if (user != null)
                {
                    userOfRecord = user;
                }

                
                    cameFrom.Clients.All.SendAsync("ReceiveMessage", "XXX", message);
                    Console.WriteLine("Resend Completed");
                

                
            }

        }




    }

    
}

