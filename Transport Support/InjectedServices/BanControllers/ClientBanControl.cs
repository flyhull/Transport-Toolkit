using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Transport_Support
{
    internal class ClientBanControl : IClientBanManager
    {
        public bool IBan
        {
            get { return true; }
        }

        public bool BanBySender(string sender)
        {
            throw new NotImplementedException();
        }

        public bool IsSenderBanned(string sender)
        {
            throw new NotImplementedException();
        }
    }
}
