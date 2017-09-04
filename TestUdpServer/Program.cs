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
            s.OnNewConnection += TcpSessionMgr.Instance.HandleNewSession;
            s.StartServiceOn(cfg);

            UdpServer us = new UdpServer();
            us.OnReceiveMessage += UDPMessageDispatcher.Instance.HandleReceiveMessage;
            us.StartServiceOn(cfg);

            CommandDispatcher.Instance.Start();
        }
    }
}
