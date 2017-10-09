using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Net;
using System.Net.Sockets;
using Base.Network;
using System.Threading.Tasks.Dataflow;
using System.Collections.Concurrent;

namespace UdpTest
{
    class Program
    {
        static string host = "127.0.0.1";
        static int    port = 12345;
        static string Ping = "Ping";
        static string Pong = "Pong";

        static bool stopFlag = false;
        static BlockingCollection<Action> taskQueue = new BlockingCollection<Action>();

        static UdpServer server;
        static UdpSession session;

        static void Main(string[] args)
        {
            startService();
            startClient();

            daemon();
        }

        static void startService()
        {
            DNS.ResolveHost(host, (s, m, address) =>
            {
                if (!s)
                {
                    Console.WriteLine("DNS解析[{0}]错误！原因：{1}", host, m);
                    return;
                }

                Socket socket = new Socket(SocketType.Dgram, ProtocolType.Udp);
                socket.Bind(new IPEndPoint(address, port));

                server = new UdpServer(socket, 100);

                server.OnMessageReceived += OnMessageReceived;
                server.StartReceive();
            });
        }

        static void startClient()
        {
            DNS.ResolveHost(host, (s, m, address) =>
            {
                if (!s)
                {
                    Console.WriteLine("DNS解析[{0}]错误！原因：{1}", host, m);
                    return;
                }

                Socket socket = new Socket(SocketType.Dgram, ProtocolType.Udp);
                socket.Connect(new IPEndPoint(address, port));

                session = new UdpSession(socket, 100);

                session.OnMessageReceived += Session_OnMessageReceived;
                session.StartReceive();

                ByteBuffer data = new ByteBuffer(1024);
                data.WriteBytes(Encoding.UTF8.GetBytes(Ping));
                session.SendMessage(0, data);
            });
        }

        private static void Session_OnMessageReceived(INetSession session, int msgID, byte[] buff)
        {
            dispatch(() =>
            {
                Console.WriteLine("Client receive: ID: {0} content: {1}", msgID, Encoding.UTF8.GetString(buff));

                ByteBuffer data = new ByteBuffer(1024);
                data.WriteBytes(Encoding.UTF8.GetBytes(Ping));
                session.SendMessage(0, data);
            });
        }

        private static void OnMessageReceived(INetSession session, int msgID, byte[] buff)
        {
            dispatch(() =>
            {
                Console.WriteLine("Service receive: ID: {0} content: {1}", msgID, Encoding.UTF8.GetString(buff));

                ByteBuffer data = new ByteBuffer(1024);
                data.WriteBytes(Encoding.UTF8.GetBytes(Pong));
                session.SendMessage(1, data);
            });
        }

        static void daemon()
        {
            Task.Run(() =>
            {
                while (!stopFlag)
                {
                    server.Update();
                    session.Update();

                    Task.Delay(10);
                }
            });

            while (!stopFlag)
            {
                var action = taskQueue.Take();
                try
                {
                    action.Invoke();
                }
                catch (Exception e)
                {
                    Console.WriteLine("执行任务出错！原因：{0}\nStackTrace: {1}", e.Message, e.StackTrace);
                }
            }
        }

        static void dispatch(Action action)
        {
            taskQueue.Add(action);
        }
    }
}
