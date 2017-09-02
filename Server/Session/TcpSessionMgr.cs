using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Net;
using System.Net.Sockets;

namespace Server
{
    class TcpSessionMgr : IDisposable
    {
        public static TcpSessionMgr Instance = new TcpSessionMgr();
        private TcpSessionMgr() { }

        public void Init()
        {
        }

        public void Dispose()
        {
            sessionDict.Clear();
        }

        public void HandleNewSession(TcpClient client)
        {
            var newId = getSessionId();
            Session newSession = new TcpSession(newId, client);
            sessionDict.TryAdd(newId, newSession);

            newSession.SetMessageDispatcher(UnAuthorizedDispatcher.Instance);
            newSession.Start();

            // Test代码
            // 向客户端发送UDP会话标识码
            newSession.SendMessage(Encoding.UTF8.GetBytes(UdpSessionMgr.Instance.GetFreeConv().ToString()));
        }

        public void HandleSessionClosed(Session s)
        {
            Session unuse;
            sessionDict.TryRemove(s.GetId(), out unuse);
        }

        static uint sessionId = 0;
        private uint getSessionId()
        {
            if (sessionId < uint.MaxValue)
            {
                return ++sessionId;
            }
            else
            {
                sessionId = 0;
                return sessionId;
            }
        }

        private ConcurrentDictionary<uint, Session> sessionDict = new ConcurrentDictionary<uint, Session>();
    }
}
