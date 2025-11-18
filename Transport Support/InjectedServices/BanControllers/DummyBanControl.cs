using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Transport_Support
{
    internal class DummyBanControl : IHubBanManager, IClientBanManager
    {
        public bool IBan
        {
            get { return false; }
        }

        public bool BanById(string connectionId)
        {
            return true;
        }

        public bool BanBySender(string sender)
        {
            return true;
        }

        public bool IsIdBanned(string connectionId)
        {
            return false;
        }

        public bool IsIpBanned(string ipAddress)
        {
            return false;
        }

        public bool IsSenderBanned(string sender)
        {
            return false;
        }
    }
}
