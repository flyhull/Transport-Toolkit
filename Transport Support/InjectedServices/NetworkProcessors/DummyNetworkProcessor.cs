using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Transport_Support
{
    public class DummyNetworkProcessor : INetworkProcessor
    {       
        public RoutedMessage ProcessInboundMessage(string sender, string message)
        {
            throw new NotImplementedException();
        }
       

    }
}
