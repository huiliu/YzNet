using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace Server
{
    class UdpSessionMgr : IDisposable
    {
        public static UdpSessionMgr Instance = new UdpSessionMgr();
        private UdpSessionMgr() { }
        public void Dispose()
        {
            sessionDict.Clear();
            sessionIdWait.Clear();
        }

        public void HandleReceiveMessage(UdpReceiveResult result, UdpServer server)
        {
            var remoteEndPoint = result.RemoteEndPoint;
            var data = result.Buffer;

            uint conv = 0;
            KCP.ikcp_decode32u(data, 0, ref conv);

            if (sessionDict.ContainsKey(conv))
            {
                // 已连接的客户端
                var session = sessionDict[conv];
                session.OnReceiveMessage(data);
            }
            else if (sessionIdWait.ContainsKey(conv))
            {
                Console.WriteLine("Udp receive: {0}", data);
                // 新连接的客户端
                OnNewConnection(conv, remoteEndPoint, server);
            }
            else
            {
                // 没有认证的客户端连接
                Console.WriteLine(string.Format("收到无效kcp/UDP包: {0}", Encoding.UTF8.GetString(data)), this.ToString());
            }
        }

        public void OnNewConnection(uint conv, IPEndPoint remoteEndPoint, UdpServer server)
        {
            var newSession = new UdpSession(conv, remoteEndPoint, server);
            sessionDict.TryAdd(conv, newSession);
            sessionIdWait.TryRemove(conv, out conv);

            newSession.Start();
        }

        // 生成一个唯一标识，用于识别kcp/UDP连接
        // TODO: 需要一个生成唯一ID的可靠方案
        private static uint conv = 0;
        public uint GetFreeConv()
        {
            while (true)
            {
                if (conv < int.MaxValue)
                    conv++;
                else
                {
                    conv = 0;
                }

                // FIXME 此处有BUG
                if (!sessionDict.ContainsKey(conv) &&
                    sessionIdWait.TryAdd(conv, conv))
                {
                    return conv;
                }
            }
        }

        private ConcurrentDictionary<uint, UdpSession> sessionDict = new ConcurrentDictionary<uint, UdpSession>();
        private ConcurrentDictionary<uint, uint> sessionIdWait = new ConcurrentDictionary<uint, uint>();
    }
}
