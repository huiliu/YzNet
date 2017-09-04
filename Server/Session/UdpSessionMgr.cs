using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace Server
{
    // UDP会话管理
    public class UdpSessionMgr : IDisposable
    {
        public event Action<UdpSession> OnSessionCreate;
        public event Action<UdpSession> OnSessionClosed;

        public static UdpSessionMgr Instance = new UdpSessionMgr();
        private UdpSessionMgr() { }

        public void Dispose()
        {
            sessionDict.Clear();
            sessionIdWait.Clear();
        }

        // 
        public UdpSession GetOrCreateUDPSession(UInt32 conv, IPEndPoint remoteEndPoint, UdpServer server)
        {
            if (sessionDict.ContainsKey(conv))
            {
                // 已连接的客户端
                return sessionDict[conv];
            }
            else if (sessionIdWait.ContainsKey(conv))
            {
                // 新连接的客户端
                OnNewConnection(conv, remoteEndPoint, server);
            }
            else
            {
                // 没有认证的客户端连接
                Console.WriteLine(string.Format("收到无效KCP/UDP包！ conv:{0}", conv), this.ToString());
            }

            return null;
        }

        // 创建一个新的UDP会话
        private void OnNewConnection(uint conv, IPEndPoint remoteEndPoint, UdpServer server)
        {
            var newSession = new UdpSession(conv, remoteEndPoint, server);
            newSession.SetMessageDispatcher(UDPMessageDispatcher.Instance);

            sessionDict.TryAdd(conv, newSession);
            sessionIdWait.TryRemove(conv, out conv);

            OnSessionCreate?.Invoke(newSession);

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
