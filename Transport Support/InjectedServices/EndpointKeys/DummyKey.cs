using Common_Support;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading.Tasks;

namespace Transport_Support
{
   
    public class DummyKey : ILocalKey
    {
        private readonly TempByteArray _userPassPhrase = new TempByteArray(Array.Empty<byte>());
        private readonly TempBytesThatHoldsDateTime _userSecretDate = new TempBytesThatHoldsDateTime(DateTime.UtcNow);


        private readonly ILogger<DummyKey> logger;

        public DummyKey(ILogger<DummyKey> loggerIn)
        {
            logger = loggerIn;

            using (logger.BeginScope("Constructing Dummy Key"))
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

        public TempByteArray UserPassPhrase
        {
            get { return _userPassPhrase; }
        }

        public TempBytesThatHoldsDateTime UserSecretDate
        {
            get { return _userSecretDate; }
        }

        public bool IntermediateNode
        {
            get { return true; }
        }
        //public DummyKey()
        //{
        //    _userPassPhrase = new TempByteArray(Array.Empty<byte>()); 
        //    _userSecretDate = new TempBytesThatHoldsDateTime(DateTime.UtcNow);
        //}
        
    }
}
