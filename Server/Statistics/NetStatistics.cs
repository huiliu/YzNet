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

        // 调用Socket.SendAsync方法次数
        public long CallSendAsyncCount { get; set; }

        // 发送包数
        // 调用TcpSession.SendMessage次数
        public long SendPacketCount { get; set; }
        
        // 收到协议包数
        // 调用MessageDispatcher.OnMessageReceived次数
        public long RecvPacketCount { get; set; }

        // 排队发送
        // 因为异步发送没有完成而排队
        public long SendByQueue { get; set; }

        public override string ToString()
        {
            return string.Format("Session[{0}]\nTotalSendBytes: {1}\nTotalRecvBytes: {2}\nCallSendAsyncCount: {3}\nSendPacket: {4}\nRecvPacket: {5}\nSendByQueue: {6}",
                s.GetId(),
                TotalSendBytes, TotalRecvBytes,
                CallSendAsyncCount,
                SendPacketCount, RecvPacketCount,
                SendByQueue);
        }

        private Session s;
    }
}
