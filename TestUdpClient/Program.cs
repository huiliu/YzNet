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
    using MessagePack;

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

            var msg = MessagePack.MessagePackSerializer.Deserialize<MsgDelayTest>(arg2);
            msg.ClientReceiveTime = Utils.IClock();
            Console.WriteLine("Udp收到消息： {0}", msg.ToString());

            msg.ClientSendTime = Utils.IClock();
            arg1.SendMessage(MessagePack.MessagePackSerializer.Serialize(msg));
        }

        private static void handleTcpOnMessageReceived(AsyncTcpClient arg1, byte[] arg2, int arg3, int arg4)
        {
            var msg = MsgUdpKey.UnPack(arg2);

            Console.WriteLine("收到UdpKey: {0}", msg.Conv);

            AsyncUdpClient uc = new AsyncUdpClient(cfg, msg.Conv);
            uc.OnMessageReceived += handleUdpOnMessageReceived;
            uc.Connect();

            MsgDelayTest buff = new MsgDelayTest();
            buff.ClientSendTime = Utils.IClock();

            uc.SendMessage(MessagePack.MessagePackSerializer.Serialize(buff));
        }
    }
}
