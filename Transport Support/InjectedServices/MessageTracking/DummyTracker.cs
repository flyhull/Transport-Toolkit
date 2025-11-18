using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Common_Support;

namespace Transport_Support
{

    
    public class DummyTracker : IMessageTracker
    {
        private readonly ILogger<DummyTracker> logger;
        public bool Tracking
        {
            get { return false; }
        }

        public int RncryptedReceiptMessageLength
        {
            get { return -1; }
        }

        public DummyTracker(ILogger<DummyTracker> loggerIn)
        {
            logger = loggerIn;

            using (logger.BeginScope("Constructing Dummy Tracker"))
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

        public ResultObject Received(byte[] contents)
        {
            throw new NotImplementedException();
        }

        public bool Sent(byte[] contenthash, string filename)
        {
            throw new NotImplementedException();
        }

        public ResultObject SendRceiptBack(byte[] contentHash)
        {
            throw new NotImplementedException();
        }

        public ResultObject SendReceiptBack(byte[] contentHash)
        {
            throw new NotImplementedException();
        }
    }
}
