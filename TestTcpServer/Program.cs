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
                AsyncTcpClient client = new AsyncTcpClient(cfg);
                client.OnConnected += handleClientOnConnected;
                client.OnMessageReceived += handleClientReceiveMessage;
                await client.Connect();
            }
        }

        private static async void handleClientOnConnected(AsyncTcpClient obj)
        {
            var temp = new string('a', 1024 * 8);
            await obj.SendMessage(Encoding.UTF8.GetBytes(temp));
        }

        private static async void handleClientReceiveMessage(AsyncTcpClient client, byte[] arg2, int arg3, int arg4)
        {
            Console.Write("*");
            RandomClose(client);
            await client.SendMessage(arg2, arg3, arg4);
        }

        private static void RandomClose(AsyncTcpClient client)
        {
            if (Utils.IClock() % 91 == 1)
            {
                Console.WriteLine("随机关闭一个客户端");
                client.Close();
            }
        }
    }
}
