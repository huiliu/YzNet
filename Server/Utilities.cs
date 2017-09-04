using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Server
{
    public static class Utils
    {
        private static readonly DateTime utc_time = new DateTime(1970, 1, 1);

        public static UInt32 IClock()
        {
            return (UInt32)(Convert.ToInt64(DateTime.UtcNow.Subtract(utc_time).TotalMilliseconds) & 0xffffffff);
        }

    }
}
