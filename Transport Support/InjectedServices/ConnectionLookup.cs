using Common_Support;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace Transport_Support
{
    public class ConnectionLookup : IConnectionLookup
    {
        private Dictionary<string, string> SenderKeyedByConnectionId = new Dictionary<string, string>();
        private Dictionary<string, string> IpAddressKeyedByConnectionId = new Dictionary<string, string>();
        private Dictionary<string, string> ConnectionIdKeyedBySender = new Dictionary<string, string>();
        private ILogger logger;

        public ConnectionLookup(ILogger<ConnectionLookup> loggerIn)
        {
            logger = loggerIn;

            using (logger.BeginScope("Constructing Connection Lookup"))
            {
                try
                {
                    logger.LogDebug("Constructed");
                }
                catch (Exception ex)
                {
                    logger.LogCritical(ex, "Encountered Exception");
                }
            }
        }

        public string InsertConnection(string ConnectionId, string IpAddress)
        {
            string result = TransportSupport.getSenderFromConnectionId(ConnectionId);

            using (logger.BeginScope("Adding connection " + ConnectionId + " associated with user " + result))
            {
                try
                {
                    if (SenderKeyedByConnectionId.ContainsKey(ConnectionId))
                    {
                        logger.LogDebug("Connection pointer to sender already exists");
                        result = string.Empty;
                    }
                    else
                    {
                        if (ConnectionIdKeyedBySender.ContainsKey(result))
                        {
                            logger.LogDebug("Sender pointer to connection already exists");
                            result = string.Empty;
                        }
                        else
                        {
                            logger.LogDebug("Adding sender pointer to connection");
                            ConnectionIdKeyedBySender.Add(result, ConnectionId);
                            logger.LogDebug("Sender pointer to connection added");
                            logger.LogDebug("Adding connection pointer to sender");
                            SenderKeyedByConnectionId.Add(ConnectionId, result);
                            logger.LogDebug("Connection pointer to sender added");
                        }                        
                    }

                    if (string.IsNullOrEmpty(IpAddress))
                    {
                        logger.LogDebug("Ip Address is unavailable");
                    }
                    else
                    {
                        logger.LogDebug("Ip Address is " + IpAddress);

                        if (IpAddressKeyedByConnectionId.ContainsKey(ConnectionId)) 
                        {
                            logger.LogDebug("Connection pointer to ip address already exists");
                            result = string.Empty;
                        }
                        else
                        {
                            logger.LogDebug("Adding connection pointer to ip address");
                            IpAddressKeyedByConnectionId.Add(ConnectionId, IpAddress);
                            logger.LogDebug("Connection pointer to ip address added");
                        }
                    }                    
                }
                catch (Exception ex)
                {
                    logger.LogCritical(ex, "Encountered Exception");
                }
            }

            return result;  
        }

        public string GetSenderByConnectionId(string ConnectionId)
        {
            string sender = string.Empty;

            using (logger.BeginScope("Getting sender for Connection Id " + ConnectionId))
            {
                try
                {
                    if (SenderKeyedByConnectionId.ContainsKey(ConnectionId))
                    {
                        sender = SenderKeyedByConnectionId[ConnectionId];
                        logger.LogDebug("Sender is " + sender);
                    }
                    else
                    {
                        logger.LogDebug("Sender is unavailable");
                    }
                }
                catch (Exception ex)
                {
                    logger.LogCritical(ex, "Encountered Exception");
                }
            }

            return sender;
        }

        public string GetConnectionIdBySender(string sender)
        {
            string result = string.Empty;

            using (logger.BeginScope("Getting Connection Id for sender " + sender))
            {
                try
                {
                    if (ConnectionIdKeyedBySender.ContainsKey(sender))
                    {
                        result = ConnectionIdKeyedBySender[sender];
                        logger.LogDebug("Connection Id is " + result);
                    }
                    else
                    {
                        logger.LogDebug("Connection Id is unavailable");
                    }
                }
                catch (Exception ex)
                {
                    logger.LogCritical(ex, "Encountered Exception");
                }
            }

            return result;
        }

        public void RemoveConnection(string ConnectionId)
        {
            using (logger.BeginScope("Removing connection " + ConnectionId))
            {
                try
                {
                    string sender = GetSenderByConnectionId(ConnectionId);

                    if (string.IsNullOrEmpty(sender))
                    {
                        logger.LogDebug("Sender is not available");
                    }
                    else
                    {
                        logger.LogDebug("Sender is " + sender);

                        if (ConnectionIdKeyedBySender.Remove(sender))
                        {
                            logger.LogDebug("Sender pointer to connection removed");
                        }
                        else
                        {
                            logger.LogDebug("Missing sender pointer to connection");
                        }
                    }

                    if (SenderKeyedByConnectionId.Remove(ConnectionId))
                    {
                        logger.LogDebug("Connection pointer to sender removed");
                    }
                    else
                    {
                        logger.LogDebug("Missing connection pointer to sender");
                    }
                

                    if (IpAddressKeyedByConnectionId.Remove(ConnectionId))
                    {
                        logger.LogDebug("Connection pointer to Ip removed");
                    }
                    else
                    {
                        logger.LogDebug("Missing connection pointer to Ip");
                    }
                }
                catch (Exception ex)
                {
                    logger.LogCritical(ex, "Encountered Exception");
                }
            }
        }

        public string GetIpAddressByConnectionId(string ConnectionId)
        {
            string IpAddress = string.Empty;

            using (logger.BeginScope("Getting Ip Address for connection " + ConnectionId))
            {
                try
                {
                    if (IpAddressKeyedByConnectionId.ContainsKey(ConnectionId))
                    {
                        IpAddress = IpAddressKeyedByConnectionId[ConnectionId];
                        logger.LogDebug("Ip address is " + IpAddress); 
                    }
                    else
                    {
                        logger.LogDebug("Ip address is unavailable");
                    }
                }
                catch (Exception ex)
                {
                    logger.LogCritical(ex, "Encountered Exception");
                }
            }

            return IpAddress;
        }
    }
}
