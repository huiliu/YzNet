using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Server;

namespace TestTcpServer
{
    class Program
    {
        static void Main(string[] args)
        {
            ServerConfig cfg = new ServerConfig();
            cfg.IP = "127.0.0.1";
            cfg.Port = 1234;

            TcpServer server = new TcpServer();
            server.OnNewConnection += TcpSessionMgr.Instance.HandleNewSession;
            server.StartServiceOn(cfg);

            startClient(10);

            CommandDispatcher.Instance.Start();
        }

        static async Task startClient(int num)
        {
            ClientCfg cfg = new ClientCfg();
            cfg.IP = "127.0.0.1";
            cfg.Port = 1234;

            for (var i = 0; i < num; ++i)
            {
                var client = await TcpConnector.ConnectTcpServer(cfg);
                client.SetMessageDispatcher(TempMessageDispatcher.Instance);
                client.IsConnected = true;
                client.CanReceive = true;

                var temp = new string('a', 1024 * 8);
                client.SendMessage(Encoding.UTF8.GetBytes(temp));
            }
        }

    }

    class TempMessageDispatcher : IMessageDispatcher
    {
        public static IMessageDispatcher Instance = new TempMessageDispatcher();
        private TempMessageDispatcher()
        {

        }

        public override void OnDisconnected(Session session)
        {
            Console.WriteLine(string.Format("[{0}]连接关闭！", session.GetId()));
        }

        public override void OnMessageReceived(Session session, byte[] data)
        {
            RandomClose(session);
            session.SendMessage(data);
        }

        public override void Start()
        {
            throw new NotImplementedException();
        }

        private void RandomClose(Session client)
        {
            if (Utils.IClock() % 10001 == 1)
            {
                Console.WriteLine("随机关闭一个客户端");
                client.Close();
            }
        }
    }
}
