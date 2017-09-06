using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace Server
{
    // 处理未认证的会话
    // 认证包括：
    // 1. 校对协议版本
    // 2. 协商加密方法
    // ...
    // 通过认证后把sesstion的MessageDispatcher设置为其它
    public class UnAuthorizedDispatcher : IMessageDispatcher
    {
        public static IMessageDispatcher Instance = new UnAuthorizedDispatcher();
        private UnAuthorizedDispatcher() { }

        public override void Start()
        {
            throw new NotImplementedException();
        }

        public void Stop()
        {
            throw new NotImplementedException();
        }

        public override void OnDisconnected(Session session)
        {
            Console.Write(string.Format("session[{0}]关闭了！", session?.GetId()));
        }

        public override void OnMessageReceived(Session session, byte[] data)
        {
            //byte[] b = new byte[count];
            //Array.Copy(data, b, count);
            //Console.WriteLine("收到消息：{0}", Encoding.UTF8.GetString(b));
            //Console.Write(session.GetId());
            session.SendMessage(data);
        }

        public void OnUdpMessageReceived(UdpReceiveResult result, UdpServer server)
        {
        }
    }
}
