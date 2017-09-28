using Microsoft.VisualStudio.TestTools.UnitTesting;
using Base.Network;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Base.Network.Tests
{
    [TestClass()]
    public class TcpServerTests
    {
        [TestMethod()]
        public void StartServiceOnTest()
        {
            TcpSession.OnMessageReceived += OnMessageReceived;
            TcpSession.OnSessionClosed += OnSessionClosed;

            TcpServer server = new TcpServer("Test");
            server.OnNewConnection += OnNewConnection;
            server.StartServiceOn("127.0.0.1", 12345);
            Assert.Fail();
        }

        private void OnSessionClosed(INetSession session)
        {
            Console.WriteLine("Session[ID: {0}]被关闭！", session.SessionID);
        }

        private void OnMessageReceived(INetSession session, int msgID, byte[] msg)
        {
            Console.WriteLine("收到Session[ID: {0}]的消息[ID: {1}]: {2}", session.SessionID, msgID, msg.ToString());
        }

        private void OnNewConnection(INetSession session)
        {
            session.SessionID = DateTime.Now.ToBinary();
            ByteBuffer buffer = new ByteBuffer();
            buffer.WriteBytes(Encoding.UTF8.GetBytes("Hello World"));
            session.SendMessage(1, buffer);
        }
    }
}