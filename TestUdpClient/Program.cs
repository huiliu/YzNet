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
            start();
            CommandDispatcher.Instance.Start();
        }

        static async Task start()
        {
            cfg = new ClientCfg();
            cfg.IP = "127.0.0.1";
            cfg.Port = 1234;

            var client = await TcpConnector.ConnectTcpServer(cfg);
            client.SetMessageDispatcher(TempMessageDispatcher.Instance);
            client.IsConnected = true;
            client.CanReceive = true;
        }


        //private static void handleTcpOnMessageReceived(TcpConnector arg1, byte[] arg2, int arg3, int arg4)
        //{
        //    var msg = MsgUdpKey.UnPack(arg2);

        //    Console.WriteLine("收到UdpKey: {0}", msg.Conv);

        //    ReliableUdpClient uc = new ReliableUdpClient(cfg, msg.Conv);
        //    uc.OnMessageReceived += handleUdpOnMessageReceived;
        //    uc.Connect();

        //    MsgDelayTest buff = new MsgDelayTest();
        //    buff.ClientSendTime = Utils.IClock();

        //    uc.SendMessage(MessagePack.MessagePackSerializer.Serialize(buff));
        //}
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

        private static long total = 0;
        public override void OnMessageReceived(Session session, byte[] data)
        {
            var msg = MsgUdpKey.UnPack(data);

            Console.WriteLine("收到UdpKey: {0}", msg.Conv);

            var cfg = new ClientCfg();
            cfg.IP = "127.0.0.1";
            cfg.Port = 1234;

            ReliableUdpClient uc = new ReliableUdpClient(cfg, msg.Conv);
            uc.OnMessageReceived += handleUdpOnMessageReceived;
            uc.Connect();

            MsgDelayTest buff = new MsgDelayTest();
            buff.ClientSendTime = Utils.IClock();

            uc.SendMessage(MessagePack.MessagePackSerializer.Serialize(buff));
        }

        public override void Start()
        {
            throw new NotImplementedException();
        }

        private void RandomClose(Session client)
        {
            if (Utils.IClock() % 91 == 1)
            {
                Console.WriteLine("随机关闭一个客户端");
                client.Close();
            }
        }

        private static void handleUdpOnMessageReceived(ReliableUdpClient arg1, byte[] arg2, int arg3, int arg4)
        {

            var msg = MessagePack.MessagePackSerializer.Deserialize<MsgDelayTest>(arg2);
            msg.ClientReceiveTime = Utils.IClock();
            Console.WriteLine("Udp收到消息： {0}", msg.ToString());

            msg.ClientSendTime = Utils.IClock();
            arg1.SendMessage(MessagePack.MessagePackSerializer.Serialize(msg));
        }
    }
}
