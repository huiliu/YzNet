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
        static void Main(string[] args)
        {
            ServerConfig cfg = new ServerConfig();
            cfg.IP = "127.0.0.1";
            cfg.Port = 1234;

            TcpServer s = new TcpServer();
            //s.OnNewConnection += handleTcpOnNewConnection; 
            s.OnNewConnection += OnNewConnection;
            s.StartServiceOn(cfg);

            UdpServer us = new UdpServer();
            us.StartServiceOn(cfg);

            CommandDispatcher.Instance.Start();
        }

        private static void OnNewConnection(System.Net.Sockets.Socket socket)
        {
            TcpSession newSession = TcpSession.Create(socket);
            // Register(newSession.GetId(), newSession);

            newSession.SetMessageDispatcher(UnAuthorizedDispatcher.Instance);
            newSession.IsConnected = true;
            newSession.CanReceive  = true;

            newSession.SendMessage(MsgUdpKey.Pack(Utils.GetConvNext()));
        }
    }
}
