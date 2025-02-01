using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace Nebula.Core
{
    public static class NetworkUtils
    {
        public static IPEndPoint ParseEndPoint(string endpoint)
        {
            var parts = endpoint.Split(':');
            return new IPEndPoint(IPAddress.Parse(parts[0]), int.Parse(parts[1]));
        }
    }
}
