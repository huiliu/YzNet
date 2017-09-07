using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
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
        static IMessageDispatcher dispatcher = new TempMessageDispatcher();
        static void Main(string[] args)
        {
            start(10);
            CommandDispatcher.Instance.Start();
        }

        static async Task start(int num)
        {
            cfg = new ClientCfg();
            cfg.IP = "127.0.0.1";
            cfg.Port = 1234;

            for (int i = 0; i < num; ++i)
            {
                var client = await TcpConnector.ConnectTcpServer(cfg);
                client.SetMessageDispatcher(dispatcher);
                client.IsConnected = true;
                client.CanReceive = true;
            }
        }
    }

    class TempMessageDispatcher : IMessageDispatcher
    {
        public TempMessageDispatcher()
        {

        }

        public override void OnDisconnected(Session session)
        {
            var rrt = rrts[session.GetId()];
            Console.WriteLine(string.Format("[{0}]连接关闭！ 收到总包数: {1} RRT: [Min: {2} Max: {3} Avg: {4}]",
                session.GetId(),
                rrt.Count,
                rrt.Min(),
                rrt.Max(),
                rrt.Average()));
        }

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

            rrts.TryAdd(msg.Conv, new List<long>());

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

        private void handleUdpOnMessageReceived(ReliableUdpClient arg1, byte[] arg2, int arg3, int arg4)
        {
            var msg = MessagePack.MessagePackSerializer.Deserialize<MsgDelayTest>(arg2);
            msg.ClientReceiveTime = Utils.IClock();

            rrts[arg1.GetID()].Add(msg.ClientReceiveTime - msg.ClientSendTime);

            msg.ClientSendTime = Utils.IClock();
            arg1.SendMessage(MessagePack.MessagePackSerializer.Serialize(msg));
        }

        private ConcurrentDictionary<uint, List<long>> rrts = new ConcurrentDictionary<uint, List<long>>();
    }
}
