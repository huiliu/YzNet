using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.IO;
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
            start(1);
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

                Task.Run(() =>
                {
                    Thread.Sleep(1000 * 60 * 3);
                    client.Close();
                });
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

            using (var fSteam = new FileStream(string.Format(@"D:\UDP{0}.txt", session.GetId()), FileMode.OpenOrCreate, FileAccess.Write))
            {
                StreamWriter sw = new StreamWriter(fSteam);

                rrt.ForEach(v =>
                {
                    sw.Write(v);
                    sw.Write('\n');
                });

                sw.Flush();
            }

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

            ReliableUdpClient uc = new ReliableUdpClient(msg.Conv);
            uc.OnMessageReceived += handleUdpOnMessageReceived;
            uc.Connect(cfg.IP, cfg.Port);

            rrts.TryAdd(msg.Conv, new List<long>());

            MsgDelayTest buff = new MsgDelayTest();
            buff.ClientSendTime = Utils.IClock();

            uc.SendMessage(MessagePack.MessagePackSerializer.Serialize(buff));
        }

        public override void Start()
        {
            throw new NotImplementedException();
        }

        private void handleUdpOnMessageReceived(ReliableUdpClient client, byte[] arg2, int arg3, int arg4)
        {
            var msg = MessagePack.MessagePackSerializer.Deserialize<MsgDelayTest>(arg2);

            var delay = Utils.IClock() - msg.ClientSendTime;
            rrts[client.GetID()].Add(delay);

            Console.WriteLine("RRT: {0}", delay);

            msg.ClientSendTime = Utils.IClock();
            client.SendMessage(MessagePack.MessagePackSerializer.Serialize(msg));
        }

        private ConcurrentDictionary<uint, List<long>> rrts = new ConcurrentDictionary<uint, List<long>>();
    }
}
