using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace TestUdpClient
{
    using Server;
    using Server.Message;

    class Program
    {
        static ClientCfg cfg;
        static void Main(string[] args)
        {
            cfg = new ClientCfg();
            cfg.IP = "127.0.0.1";
            cfg.Port = 1234;

            AsyncTcpClient client = new AsyncTcpClient(cfg);
            client.OnMessageReceived += handleTcpOnMessageReceived;
            client.Connect();

            CommandDispatcher.Instance.Start();
        }

        private static void handleUdpOnMessageReceived(AsyncUdpClient arg1, byte[] arg2, int arg3, int arg4)
        {
            Console.WriteLine("Udp收到消息： {0}", Encoding.UTF8.GetString(arg2));
        }

        private static void handleTcpOnMessageReceived(AsyncTcpClient arg1, byte[] arg2, int arg3, int arg4)
        {
            var msg = MsgUdpKey.UnPack(arg2);

            Console.WriteLine("收到UdpKey: {0}", msg.Conv);

            AsyncUdpClient uc = new AsyncUdpClient(cfg, msg.Conv);
            uc.OnMessageReceived += handleUdpOnMessageReceived;
        }
    }
}
