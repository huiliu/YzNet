using System;
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

        public override void OnDisconnected(INetSession session)
        {
            throw new NotImplementedException();
        }

        public override void OnMessageReceived(INetSession session, byte[] data)
        {
            // TODO: 测试代码
            var m = MessagePackSerializer.Deserialize<MsgDelayTest>(data);
            m.ServerReceiveTime = Utils.IClock();

            session.SendMessage(MessagePackSerializer.Serialize(m));
        }
    }
}
