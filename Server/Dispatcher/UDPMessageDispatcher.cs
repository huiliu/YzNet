﻿using System;
using System.Net.Sockets;
using System.Threading.Tasks;

using MessagePack;
using Server.Message;

namespace Server
{
    public class UDPMessageDispatcher : IMessageDispatcher
    {
        public static UDPMessageDispatcher Instance = new UDPMessageDispatcher();
        private UDPMessageDispatcher() { }

        public override void Start()
        {
            throw new NotImplementedException();
        }

        public void Stop()
        {
            throw new NotImplementedException();
        }

        // 处理收到的UDP消息
        public void HandleReceiveMessage(UdpReceiveResult result, UdpServer server)
        {
            var remoteEndPoint = result.RemoteEndPoint;
            var data = result.Buffer;

            uint conv = 0;
            KCP.ikcp_decode32u(data, 0, ref conv);

            UdpSession session = UdpSessionMgr.Instance.GetOrCreateUDPSession(conv, remoteEndPoint, server);
            if (session != null)
            {
                session.OnReceiveMessage(data);
            }
        }

        public override void OnDisconnected(Session session)
        {
            throw new NotImplementedException();
        }

        public override void OnMessageReceived(Session session, byte[] data)
        {
            // TODO: 测试代码
            var m = MessagePackSerializer.Deserialize<MsgDelayTest>(data);
            m.ServerReceiveTime = Utils.IClock();

            session.SendMessage(MessagePackSerializer.Serialize(m));
        }
    }
}
