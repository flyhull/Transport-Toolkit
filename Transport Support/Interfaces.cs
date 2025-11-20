using Common_Support;
using Microsoft.AspNetCore.Connections;
using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using SixLabors.ImageSharp.PixelFormats;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Transport_Support
{
    public interface IMessageTracker
    {
        bool Tracking
        {
            get;
        }

        Int32 EncryptedReceiptMessageLength
        {
            get;
        }

        bool Sent(byte[] contentHash, string filename);

        ResultObject SendReceiptBack(byte[] contentHash);

        ResultObject Received(byte[] contents);
    }

    public interface ILocalKey
    {
        TempByteArray UserPassPhrase
        {
            get;
        }

        TempBytesThatHoldsDateTime UserSecretDate
        {
            get;
        }   
        
        bool IntermediateNode
        {
            get;
        }
      
    }

    public enum ExposedHubAction
    {
        AddMeToGroup,
        SendMessageToGroup,
        BanUser
    }

    public enum ExposedClientAction
    {
        ReceiveMessage
    }

    public interface IChatHub
    {
        Task<string> SendMessageToGroup(string sender, string message, string group);
        Task<string> AddMeToGroup(string group);
        Task<string> BanUser(string user);

    }

    public interface IHubProcessor
    {
        RoutedMessage ProcessMessage(string base64In, string connectionId , string group , string url);
    }

    public interface IClientConnection
    {
        RoutedMessageStatus SendMessage(RoutedMessage message);
        RoutedMessageStatus ProcessFile(string filename);
        RoutedMessageStatus BanSender(string sender);
        bool IsConnected();
        void Disconnect();       
    }

    public interface INetworkProcessor
    {
        RoutedMessage ProcessInboundMessage(string sender, string message);

    }
    public interface IFileProcessor
    {
        RoutedMessage ProcessOutboundFile(string fileName);

        List<RoutedMessageStatus> GetSuccessList
        {
            get;
        }
    }

    public enum Purpose
    {
        Transmitter,
        Hub,
        Reflector,
        Relay,
        Receiver,
        none,
        Client
    }

    public interface IUsage
    {   Purpose ProgramPurpose
        {
            get;
        }

        String WatchedSubDirectory 
        {
            get;
        }

        String TempSubDirectory
        {
            get;
        }

        String OutputSubDirectory
        {
            get;
        }

    }

    // used per transaction
    public interface IRouteProvider
    {
        bool Valid
        {
            get;
        }

        WayPoint GetFrom(string group = "");
        WayPoint GetTo(string group = "");
        bool HasRoute(string group = "");
        bool Reflect(string group = "");
        public string Snapshot();
        public List<string> Spill();      
    }

    public interface IHubBanManager
    {
        public bool IsIpBanned(string ipAddress);
        public bool IsIdBanned(string connectionId);
        public bool BanById(string connectionId);
        public bool IsSenderBanned(string sender);
        public bool BanBySender(string sender);
        bool IBan
        {
            get;
        }
    }

    public interface IClientBanManager
    {
        public bool IsSenderBanned(string sender);
        public bool BanBySender(string sender);
        bool IBan
        {
            get;
        }

    }

    public interface IDuplicateManager
    {          
        public bool IsDuplicate(byte[] hashOfByteContents, string fromGroup); 
    }

    // per transaction flow logic

    public interface IExtractor
    {
        public ResultObject Extract(string input, DateTime timestamp, WayPoint origin, bool forTemp, Image_Support.ImageOutputFormat format, string subDirectoryName = "Error");

        public ResultObject Extract(ResultObject input, DateTime timestamp, WayPoint origin, bool forTemp, Image_Support.ImageOutputFormat format, string subDirectoryName = "Error");
    }

    public interface IEmbedder
    {
        public ResultObject Embed(ref ResultObject input, WayPoint destination, DateTime encryptionTime, bool forTemp, Image_Support.ImageOutputFormat format, string subDirectoryName = "Error");
    }

    

    public interface IConnectionLookup
    {
        string InsertConnection(string ConnectionId, string IpAddress);
        string GetSenderByConnectionId(string ConnectionId);
        string GetConnectionIdBySender(string RandomString);
        public void RemoveConnection(string ConnectionId);
        public string GetIpAddressByConnectionId(string ConnectionId);

    }

    public interface ITempFileManager
    {
        ResultObject StoreBytes(ResultObject input);  
        ResultObject GetBase64(string filename);
        ResultObject DeleteFile(string filename);

    }

    //public delegate void CacheRoutedMessageRemoved(object messageId, object? message, EvictionReason evictionReason, object? state);




}
