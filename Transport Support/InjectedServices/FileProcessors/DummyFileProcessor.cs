using Common_Support;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using SixLabors.ImageSharp.PixelFormats;
using System;
using System.Buffers.Text;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Transport_Support
{
    public class DummyFileProcessor : IFileProcessor
    {
        public List<RoutedMessageStatus> GetSuccessList 
        {
            get { return new List<RoutedMessageStatus>();}
        }

        public RoutedMessage ProcessOutboundFile(string fileName)
        {
            throw new NotImplementedException();
        }
    }
}
