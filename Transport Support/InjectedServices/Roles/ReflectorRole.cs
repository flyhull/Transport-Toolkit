using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Transport_Support
{
    public class ReflectorRole : IUsage
    {
        public Purpose ProgramPurpose
        {
            get { return Purpose.Reflector; }
        }

        public string WatchedSubDirectory => throw new NotImplementedException();

        public string TempSubDirectory
        {
            get { return "Temp"; }
        }

        public string OutputSubDirectory => throw new NotImplementedException();
    }
}
