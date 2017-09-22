using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Base.Network
{
    // 表示一个会话，可能是TCP，也可能是UDP
    public abstract class INetSession
    {
        public uint SessionID { get; set; }

        public abstract void Close();

        // 向对端发送buffer
        public abstract void SendMessage(int msgID, ByteBuffer buffer);

        // 开始接收数据
        public abstract void StartReceive();
    }
}
