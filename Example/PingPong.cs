using System;
using System.Diagnostics;
using System.Text;
using System.Threading.Tasks;
using Base.Network;

namespace Example
{
    public class PingPong
    {
        private static readonly string ip = "127.0.0.1";
        private static readonly int port = 12345;
        private const long ClientSessionID = 0;
        private const long ServerSessionID = 1;

        public PingPong()
        {
            TcpSession.OnMessageReceived += OnMessageReceived;
            TcpSession.OnSessionClosed += OnSessionClosed;
        }

        public void Run()
        {
            StartPingPongService();
            Task.Delay(500);
            StartPingPongClient();
        }

        private void StartPingPongService()
        {
            TcpServer server = new TcpServer("Test");
            server.OnNewConnection += OnNewConnection;
            server.StartServiceOn(ip, port);
        }

        private void StartPingPongClient()
        {
            TcpSession session = null;
            Connector c = new Connector();
            c.ConnectServer(ip, port, (succ, m, s) =>
            {
                if (!succ)
                {
                    Console.WriteLine("连接[{0}/{1}]失败！", ip, port);
                    return;
                }

                Debug.Assert(s != null, "数据错误！返回的socket为null", "ConnectorCB");
                Console.WriteLine("连接[{0}/{1}]成功！", ip, port);
                session = new TcpSession(s)
                {
                    SessionID = ClientSessionID,
                };

                session.StartReceive();

                Task.Run(() =>
                {
                    while(true)
                    {
                        ByteBuffer pingBuffer = new ByteBuffer();
                        pingBuffer.WriteBytes(Encoding.UTF8.GetBytes("Ping"));
                        session.SendMessage(0, pingBuffer);
                        Task.Delay(100);
                    }
                });
            });
        }

        private void OnNewConnection(INetSession session)
        {
            Console.WriteLine("New Session Come in");

            session.SessionID = ServerSessionID;
            session.StartReceive();

            ByteBuffer pingBuffer = new ByteBuffer();
            pingBuffer.WriteBytes(Encoding.UTF8.GetBytes("Ping"));
            session.SendMessage(0, pingBuffer);
        }

        private void OnSessionClosed(INetSession session)
        {
            Console.WriteLine("Session[ID: {0}]被关闭！", session.SessionID);
        }

        private void OnMessageReceived(INetSession session, int msgID, byte[] msg)
        {
            Console.WriteLine("收到Session[ID: {0}]的消息[ID: {1}]: {2}", session.SessionID, msgID, Encoding.UTF8.GetString(msg));

            ByteBuffer pingBuffer = new ByteBuffer();
            pingBuffer.WriteBytes(Encoding.UTF8.GetBytes("Ping"));
            session.SendMessage(0, pingBuffer);
        }
    }
}
