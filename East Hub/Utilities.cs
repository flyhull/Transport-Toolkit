using Microsoft.AspNetCore.SignalR;

namespace GalleryTwo
{
    public class UserNameProvider : IUserIdProvider
    {
        public string GetUserId(HubConnectionContext connection)
        {
            return connection.ConnectionId;
        }
    }

}
