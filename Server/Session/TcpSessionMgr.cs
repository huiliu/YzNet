using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Net;
using System.Net.Sockets;

using MessagePack;
using Server.Message;

namespace Server
{
    // 管理TCP会话连接
    public class TcpSessionMgr : IDisposable
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

        public void Register(uint id, TcpSession session)
        {
            lock(sessionDict)
            {
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
            lock (sessionDict)
            {
                if (sessionDict.ContainsKey(id))
                {
                    sessionDict.Remove(id);
                }
            }
        }

        public void HandleNewSession(Socket socket)
        {
            TcpSession newSession = TcpSession.Create(socket);
            Register(newSession.GetId(), newSession);

            newSession.SetMessageDispatcher(UnAuthorizedDispatcher.Instance);
            newSession.IsConnected = true;
            newSession.CanReceive  = true;

            // Test代码
            // 向客户端发送UDP会话标识码
            // newSession.SendMessage(Encoding.UTF8.GetBytes(UdpSessionMgr.Instance.GetFreeConv().ToString()));
            var key = MsgUdpKey.Pack(1);
            newSession.SendMessage(MsgUdpKey.Pack(UdpSessionMgr.Instance.GetFreeConv()));
        }

        public void HandleSessionClosed(uint id)
        {
            UnRegister(id);
        }

        public TcpSession FindSessionById(uint sessionId)
        {
            TcpSession session = null;
            sessionDict.TryGetValue(sessionId, out session);

            return session;
        }

        static uint sessionId = 0;
        public uint getSessionId()
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

        private Dictionary<uint, TcpSession> sessionDict = new Dictionary<uint, TcpSession>();
    }
}
