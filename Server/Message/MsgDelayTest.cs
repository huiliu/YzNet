using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Server.Message
{
    using MessagePack;

    [MessagePackObject(keyAsPropertyName:true)]
    public class MsgDelayTest
    {
        public MsgDelayTest()
        {
            ClientReceiveTime = 0;
            ClientSendTime = 0;
            ServerReceiveTime = 0;
            ServerSendTime = 0;

            var temp = new string('x', 512);
            Buffer = Encoding.UTF8.GetBytes(temp);
        }

        public long ClientSendTime    { get; set; }
        public long ClientReceiveTime { get; set; }
        public long ServerSendTime    { get; set; }
        public long ServerReceiveTime { get; set; }
        public byte[] Buffer { get; private set; }

        public override string ToString()
        {
            return string.Format("RRT: {0} BufferLength: {1}",
                ClientReceiveTime - ClientSendTime,
                Buffer.Length);
        }
    }
}
