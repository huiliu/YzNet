using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Net;
using System.Net.Sockets;

namespace Server
{
    class SessionMgr : IDisposable
    {
        public static SessionMgr Instance = new SessionMgr();
        private SessionMgr() { }

        public void Init()
        {
        }

        public void Dispose()
        {
            sessionDict.Clear();
        }

        public void CreateTcpSession(TcpClient client)
        {

            var newId = getSessionId();
            Session newSession = new TcpSession(newId, client);
            sessionDict.TryAdd(newId, newSession);

            newSession.SetMessageDispatcher(UnAuthorizedDispatcher.Instance);
            newSession.Start();
        }

        public void HandleSessionClosedEvent(Session s)
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
