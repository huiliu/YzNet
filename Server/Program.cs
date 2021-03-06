﻿using System;
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
            us.StartServiceOn(cfg);

            ClientCfg clientCfg = new ClientCfg();
            clientCfg.IP = "127.0.0.1";
            clientCfg.Port = 1234;

            ReliableUdpClient uc = new ReliableUdpClient(1);
            uc.Connect(clientCfg.IP, clientCfg.Port);

            uc.SendMessage(Encoding.UTF8.GetBytes("Hello World!"));

            CommandDispatcher.Instance.Start();
        }
    }
}
