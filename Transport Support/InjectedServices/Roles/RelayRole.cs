using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Transport_Support
{
    public class RelayRole : IUsage
    {
        public Purpose ProgramPurpose
        {
            get { return Purpose.Relay; }
        }
       
        public string WatchedSubDirectory
        {
            get { return "Temp"; }
        }

        public string TempSubDirectory
        {
            get { return "Temp"; }
        }

        public string OutputSubDirectory => throw new NotImplementedException();
    }
}
