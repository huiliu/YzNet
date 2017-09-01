using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace Server
{
    class UdpSession : Session
    {
        public UdpSession(IPEndPoint clientEndPoint)
        {
            this.remoteEndPoint = clientEndPoint;
        }

        public void Start()
        {
            throw new NotImplementedException();
        }

        public void Close()
        {
            throw new NotImplementedException();
        }

        public void SetMessageDispatcher(MessageDispatcher dispatcher)
        {
            throw new NotImplementedException();
        }

        public Task SendMessage(byte[] buffer)
        {
            throw new NotImplementedException();
        }

        public Task SendMessage(byte[] buffer, int offset, int count)
        {
            throw new NotImplementedException();
        }

        public uint GetId()
        {
            return id;
        }

        private uint id;
        private UdpClient server;
        private IPEndPoint remoteEndPoint;
    }
}
