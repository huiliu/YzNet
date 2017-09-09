using System;
using System.Net.Sockets;

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

        public override void OnDisconnected(INetSession session)
        {
            Console.Write(string.Format("session[{0}]关闭了！", session?.GetId()));
        }

        public override void OnMessageReceived(INetSession session, byte[] data)
        {
            session.SendMessage(data);
        }
    }
}
