using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Server
{
    // 统计网络相关信息
    class NetStatistics : IDisposable
    {

        public NetStatistics(Session session)
        {
            s = session;
        }

        public void Dispose()
        {
        }

        public void Close()
        {
            Console.WriteLine(ToString());
        }

        public long TotalSendBytes { get; set; }
        public long TotalRecvBytes { get; set; }

        public long SendPacketCount { get; set; }
        public long RecvPacketCount { get; set; }

        public override string ToString()
        {
            return string.Format("Session[{0}]\nTotalSendBytes: {1}\nTotalRecvBytes: {2}\nSendPacket: {3}\nRecvPacket: {4}\n",
                s.GetId(),
                TotalSendBytes, TotalRecvBytes,
                TotalSendBytes, RecvPacketCount);
        }

        private Session s;
    }
}
