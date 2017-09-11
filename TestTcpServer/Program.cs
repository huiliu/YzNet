using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using Server;
using Server.Message;
using MessagePack;
using System.Timers;

namespace TestTcpServer
{
    class Program
    {
        static void Main(string[] args)
        {
            ServerConfig cfg = new ServerConfig();
            cfg.IP = "127.0.0.1";
            cfg.Port = 12345;

            TcpServer server = new TcpServer();
            server.OnNewConnection += TcpSessionMgr.Instance.HandleNewSession;
            server.StartServiceOn(cfg);

            // startClient(1);
            Console.WriteLine("服务启动成功！");
            CommandDispatcher.Instance.Start();
        }

        static async Task startClient(int num)
        {
            ClientCfg cfg = new ClientCfg();
            cfg.IP = "127.0.0.1";
            cfg.Port = 12345;

            TempMessageDispatcher dispatcher = new TempMessageDispatcher();

            for (var i = 0; i < num; ++i)
            {
                INetSession session = null;
                var connector = new TcpConnector();
                connector.ConnectServer(cfg.IP, cfg.Port, (INetSession s, bool succ, string errmsg) =>
                {
                    if (!succ)
                    {
                        Console.WriteLine("连接失败！");
                        return;
                    }

                    session = s;

                    session.SetMessageDispatcher(dispatcher);
                    session.IsConnected = true;
                    session.CanReceive = true;
                    dispatcher.rrts.TryAdd(session.GetId(), new List<long>());

                    var msg = new MsgDelayTest();
                    msg.ClientSendTime = Utils.IClock();
                    session.SendMessage(MessagePack.MessagePackSerializer.Serialize(msg));

                    Task.Run(() =>
                    {
                        Thread.Sleep(1000 * 60 * 3);
                        session.Close();
                    });
                });
            }
        }

    }

    class TempMessageDispatcher : IMessageDispatcher
    {
        public TempMessageDispatcher()
        {

        }

        public override void OnDisconnected(INetSession session)
        {
            var rrt = rrts[session.GetId()];

            using (var fSteam = new FileStream(string.Format(@"D:\TCP{0}.txt", session.GetId()), FileMode.OpenOrCreate, FileAccess.Write))
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
            Console.WriteLine(string.Format("[{0}]连接关闭！", session.GetId()));
        }

        public override void OnMessageReceived(INetSession session, byte[] data)
        {
            Console.WriteLine(string.Format("received message: {0}", BitConverter.ToSingle(data, 0)));
            session.SendMessage(data);
            //var msg = MessagePack.MessagePackSerializer.Deserialize<MsgDelayTest>(data);

            //var delay = Utils.IClock() - msg.ClientSendTime;
            //rrts[session.GetId()].Add(delay);

            //Console.WriteLine("RRT: {0}", delay);

            //msg.ClientSendTime = Utils.IClock();
            //var buff = MessagePackSerializer.Serialize(msg);
            //session.SendMessage(buff);
        }

        public override void Start()
        {
            throw new NotImplementedException();
        }

        private bool RandomClose(INetSession client)
        {
            if (Utils.IClock() % 10001 == 1)
            {
                Console.WriteLine("随机关闭一个客户端");
                client.Close();
                return true;
            }

            return false;
        }

        public ConcurrentDictionary<uint, List<long>> rrts = new ConcurrentDictionary<uint, List<long>>();
    }
}
