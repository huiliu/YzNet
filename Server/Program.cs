using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Server
{
    class Program
    {
        static void Main(string[] args)
        {
            ServerConfig cfg = new ServerConfig();
            cfg.IP = "127.0.0.1";
            cfg.Port = 1234;

            TcpServer s = new TcpServer();
            s.OnNewConnection += TcpSessionMgr.Instance.HandleNewSession;
            s.StartServiceOn(cfg);


            UdpServer us = new UdpServer();
            us.OnReceiveMessage += UdpSessionMgr.Instance.HandleReceiveMessage;
            us.StartServiceOn(cfg);

            CommandDispatcher.Instance.Start();
        }
    }
}
