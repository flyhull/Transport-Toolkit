using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Transport_Support
{
    public class TransmitterRole : IUsage
    {
        public Purpose ProgramPurpose
        {
            get { return Purpose.Transmitter; }
        }
        public string WatchedSubDirectory
        {
            get { return "Outgoing"; }
        }
        public string TempSubDirectory => throw new NotImplementedException();

        public string OutputSubDirectory => throw new NotImplementedException();
    }
}
