using Common_Support;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Transport_Support
{
    public class DummyFileManager : ITempFileManager
    {
        private readonly ILogger<DummyFileManager> logger;

        public DummyFileManager(ILogger<DummyFileManager> loggerIn)
        {
            logger = loggerIn;

            using (logger.BeginScope("Constructing Dummy File Manager"))
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

        public ResultObject DeleteFile(string filename)
        {
            throw new NotImplementedException();
        }

        public ResultObject GetBase64(string filename)
        {
            throw new NotImplementedException();
        }

        public ResultObject StoreBytes(ResultObject input)
        {
            throw new NotImplementedException();
        }

    }
}
