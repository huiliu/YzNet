using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace Server
{
    // UDP会话管理
    public class UdpSessionMgr : IDisposable
    {
        public event Action<uint>   OnSessionClosed;

        public static UdpSessionMgr Instance = new UdpSessionMgr();
        private UdpSessionMgr()
        {
            syncRoot = new object();
        }

        public void Dispose()
        {
            sessionDict.Clear();
            sessionIdWait.Clear();
        }

        public void Register(uint id, UdpSession session)
        {
            lock(syncRoot)
            {
                if (!sessionIdWait.ContainsKey(id))
                {
                    // ID 不在池中，认为是非法的
                    session.Close();
                    return;
                }

                if (sessionDict.ContainsKey(id))
                {
                    Debug.WriteLine(string.Format("出现相同SessionId[{0}]", id), "TcpSession");
                    var oldSession = sessionDict[id];
                    oldSession.Close();
                }

                sessionDict[id] = session;
            }
        }

        public void UnRegister(uint id)
        {
            lock (syncRoot)
            {
                if (sessionDict.ContainsKey(id))
                {
                    sessionDict.Remove(id);
                }
            }
        }

        public UdpSession GetOrCreateUDPSession(UInt32 conv, IPEndPoint remoteEndPoint, UdpServer server)
        {
            bool isNew = false;

            lock(syncRoot)
            {
                if (sessionDict.ContainsKey(conv))
                {
                    // 已连接的客户端
                    return sessionDict[conv];
                }
                else if (sessionIdWait.ContainsKey(conv))
                {
                    // 新连接
                    isNew = true;
                }
                else
                {
                    // 没有认证的客户端连接
                    Console.WriteLine(string.Format("收到无效KCP/UDP包！ conv:{0}", conv), this.ToString());
                }
            }

            if (isNew)
            {
                // 新连接的客户端
                OnNewConnection(conv, remoteEndPoint, server);
            }

            return null;
        }

        public UdpSession FindSessionById(uint sessionId)
        {
            UdpSession session = null;
            sessionDict.TryGetValue(sessionId, out session);

            return session;
        }

        // 创建一个新的UDP会话
        private void OnNewConnection(uint conv, IPEndPoint remoteEndPoint, UdpServer server)
        {
            var newSession = UdpSession.Create(conv, remoteEndPoint, server);
            Register(conv, newSession);

            newSession.SetMessageDispatcher(UDPMessageDispatcher.Instance);
            newSession.IsConnected = true;
            newSession.CanReceive = true;
        }

        // 生成一个唯一标识，用于识别kcp/UDP连接
        // TODO: 需要一个生成唯一ID的可靠方案
        private static uint conv = 0;
        public uint GetFreeConv()
        {
            while (true)
            {
                if (conv < uint.MaxValue)
                    conv++;
                else
                {
                    conv = 0;
                }

                lock(syncRoot)
                {
                    if (!sessionDict.ContainsKey(conv) &&
                        !sessionIdWait.ContainsKey(conv))
                    {
                        sessionIdWait.Add(conv, Utils.IClock());
                        return conv;
                    }
                }
            }
        }

        private object syncRoot;
        private Dictionary<uint, UdpSession> sessionDict = new Dictionary<uint, UdpSession>();
        private Dictionary<uint, uint> sessionIdWait = new Dictionary<uint, uint>();
    }
}
