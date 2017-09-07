using System;
using System.Collections.Generic;
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
        public event Action<uint>   OnSessionClosed;

        public UdpSessionMgr()
        {
            syncRoot = new object();
        }

        public void Dispose()
        {
            sessionDict.Clear();
            sessionIdWait.Clear();
            endPoint2Session.Clear();
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

        public UdpSession FindUdpSessionByEndPoint(EndPoint endPoint)
        {
            UdpSession session = null;
            endPoint2Session.TryGetValue(endPoint, out session);

            return session;
        }

        public void OnMessageReceived(UdpServer server, EndPoint endPoint, byte[] data, int size)
        {
            UdpSession session;
            if (!endPoint2Session.TryGetValue(endPoint, out session))
            {
                // 新连接
                uint conv = 0;
                KCP.ikcp_decode32u(data, 0, ref conv);
                //if (VerifyConv(conv))
                //{
                    session = OnNewConnection(conv, endPoint, server);
                //}
            }

            Debug.Assert(session != null, "UDP会话不存在！", "UDPSessionMgr");

            // TODO: 优化，减少拷贝
            byte[] temp = new byte[size];
            Array.Copy(data, 0, temp, 0, size);

            session.OnReceiveMessage(temp);
        }

        // 创建一个新的UDP会话
        private UdpSession OnNewConnection(uint conv, EndPoint remoteEndPoint, UdpServer server)
        {
            var newSession = UdpSession.Create(conv, remoteEndPoint, server);
            //Register(conv, newSession);
            endPoint2Session.TryAdd(remoteEndPoint, newSession);

            newSession.SetMessageDispatcher(UDPMessageDispatcher.Instance);
            newSession.IsConnected = true;
            newSession.CanReceive = true;

            return newSession;
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

        // 验证会话ID是否有效
        private bool VerifyConv(uint conv)
        {
            lock(syncRoot)
            {
                if (!sessionDict.ContainsKey(conv) &&
                    sessionIdWait.ContainsKey(conv))
                {
                    return true;
                }
            }

            return false;
        }

        private object syncRoot;
        private Dictionary<uint, UdpSession> sessionDict = new Dictionary<uint, UdpSession>();
        private Dictionary<uint, uint> sessionIdWait = new Dictionary<uint, uint>();
        private ConcurrentDictionary<EndPoint, UdpSession> endPoint2Session = new ConcurrentDictionary<EndPoint, UdpSession>();
    }
}
