using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TestUdpServer
{
    using Server;
    using Server.Message;
    using MessagePack;

    class Program
    {
        static Dictionary<UInt32, UdpSession> sessions = new Dictionary<uint, UdpSession>();
        static void Main(string[] args)
        {
            {
                var ms = new MemoryStream();
                var buff = MsgUdpKey.Pack(1);
                ms.Write(buff, 0, buff.Length);

                ms.Position = 0;
                MsgUdpKey.UnPack(ms.GetBuffer());
            }
            {
                var buff = MessagePack.MessagePackSerializer.Serialize(new MsgPing(1));
                var m = MessagePack.MessagePackSerializer.Deserialize<MsgPing>(buff);
            }

            ServerConfig cfg = new ServerConfig();
            cfg.IP = "127.0.0.1";
            cfg.Port = 1234;

            TcpServer s = new TcpServer();
            s.OnNewConnection += handleTcpOnNewConnection; 
            s.StartServiceOn(cfg);

            UdpServer us = new UdpServer();
            us.OnReceiveMessage += handleUdpOnReceiveMessage;
            us.StartServiceOn(cfg);

            CommandDispatcher.Instance.Start();
        }

        private static void handleUdpOnReceiveMessage(System.Net.Sockets.UdpReceiveResult arg1, UdpServer arg2)
        {
            var data = arg1.Buffer;
            var remoteEndPoint = arg1.RemoteEndPoint;

            UInt32 conv = 0;
            KCP.ikcp_decode32u(data, 0, ref conv);

            if (sessions.ContainsKey(conv))
            {
                var session = sessions[conv];
                session.OnReceiveMessage(arg1.Buffer);
            }
            else
            {
                Console.WriteLine("new client Conv: {0}", conv);

                var client = new UdpSession(conv, remoteEndPoint, arg2);
                client.OnMessageReceived += handleUdpOnMessageReceived;
                client.Start();
                sessions.Add(conv, client);
            }

        }

        private static void handleUdpOnMessageReceived(UdpSession arg1, byte[] arg2)
        {
            var m = MessagePackSerializer.Deserialize<MsgDelayTest>(arg2);
            m.ServerReceiveTime = Utils.IClock();
            arg1.SendMessage(MessagePackSerializer.Serialize(m));
        }

        private static void handleTcpOnNewConnection(System.Net.Sockets.TcpClient client)
        {
            Task.Run(async () =>
            {
                var stream = client.GetStream();

                // 发送UDP Key
                var key = MsgUdpKey.Pack(1);
                await stream.WriteAsync(key, 0, key.Length);

                // 接收消息
                while (true)
                {
                    byte[] buffer = new byte[1024];
                    try
                    {
                        var count = await stream.ReadAsync(buffer, 0, 1024);
                        if (count ==0)
                        {
                            client.Close();
                            break;
                        }
                    }
                    catch (Exception e)
                    {
                        client.Close();
                        Console.Write("Catch Exception: {0}", e.Message);
                        break;
                    }

                    Console.WriteLine("收到网络消息：{0}", Encoding.UTF8.GetString(buffer));
                }
            });
        }
    }
}
