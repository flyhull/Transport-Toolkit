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
   
    public class LocalKey : ILocalKey, IDisposable
    {
        private readonly TempByteArray _userPassPhrase;
        private readonly TempBytesThatHoldsDateTime _userSecretDate;
        private readonly string module = "message";
        private readonly ILogger<LocalKey> logger;

        private bool disposedValue;

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
            get { return false; }
        }
        public LocalKey(ILogger<LocalKey> loggerIn)
        {
            logger = loggerIn;

            using (logger.BeginScope("Constructing Local Key"))
            {
                try
                {
                    _userPassPhrase = new TempByteArray(TransportSupport.GetPassphrase(module));
                    _userSecretDate = new TempBytesThatHoldsDateTime(TransportSupport.GetSecretDateTime(module));
                    logger.LogDebug("Constructed");
                }
                catch (Exception ex)
                {
                    _userPassPhrase = new TempByteArray(Array.Empty<byte>());
                    _userSecretDate = new TempBytesThatHoldsDateTime(DateTime.UtcNow);
                    logger.LogCritical(ex, "Encountered Exception");
                }
            }
        }
        

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    // TODO: dispose managed state (managed objects)
                }

                // TODO: free unmanaged resources (unmanaged objects) and override finalizer
                // TODO: set large fields to null
                disposedValue = true;
            }
        }

        // // TODO: override finalizer only if 'Dispose(bool disposing)' has code to free unmanaged resources
        // ~LocalKey()
        // {
        //     // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
        //     Dispose(disposing: false);
        // }

        void IDisposable.Dispose()
        {
            // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }
    }
}
