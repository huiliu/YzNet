using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TestUdpServer
{
    using Server;
    using Server.Message;
    using MsgPack.Serialization;

    class Program
    {
        static void Main(string[] args)
        {
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
            Console.WriteLine("Conv: {0}", conv);

            var client = new UdpSession(conv, remoteEndPoint, arg2);
            client.SetMessageDispatcher(UnAuthorizedDispatcher.Instance);
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

                    var count = await stream.ReadAsync(buffer, 0, 1024);
                    if (count ==0)
                    {
                        break;
                    }

                    Console.WriteLine("收到网络消息：{0}", Encoding.UTF8.GetString(buffer));
                }
            });
        }
    }
}
