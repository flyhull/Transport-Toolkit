using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Transport_Support
{
    public class ClientRole : IUsage
    {
        public Purpose ProgramPurpose
        {
            get { return Purpose.Client; }
        }
        public string WatchedSubDirectory
        {
            get { return "Outgoing"; }
        }
        public string TempSubDirectory
        {
            get { return "Error"; }
        }
        public string OutputSubDirectory
        {
            get { return "Received"; }
        }
    }
}
