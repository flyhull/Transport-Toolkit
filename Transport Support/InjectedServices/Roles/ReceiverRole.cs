using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Transport_Support
{
    public class ReceiverRole : IUsage
    {
        public Purpose ProgramPurpose
        {
            get { return Purpose.Receiver; }
        }

        public string WatchedSubDirectory => throw new NotImplementedException();

        public string TempSubDirectory => throw new NotImplementedException();
        public string OutputSubDirectory
        {
            get { return "Received"; }
        }
    }
}
