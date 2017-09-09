using System;
using System.Collections.Generic;

namespace YezhStudio.Base.Network
{
    public static class Utils
    {
        private static readonly DateTime utc_time = new DateTime(1970, 1, 1);

        public static UInt32 IClock()
        {
            return (UInt32)(Convert.ToInt64(DateTime.UtcNow.Subtract(utc_time).TotalMilliseconds) & 0xffffffff);
        }

        // TODO: 安全的产生一个cookie用于UDP标识
        public static uint GetConvNext()
        {
            lock (ConvSync)
            {
                while (currentConv < uint.MaxValue)
                {
                    ++currentConv;

                    if (!udpConv.ContainsKey(currentConv))
                    {
                        return currentConv;
                    }
                }
            }

            return 0;
        }

        public static void ReturnConv(uint conv)
        {
            lock (ConvSync)
            {
                udpConv.Remove(conv);
            }
        }

        private static object ConvSync = new object();
        private static uint currentConv = 0;
        private static Dictionary<uint, uint> udpConv = new Dictionary<uint, uint>();
    }
}
