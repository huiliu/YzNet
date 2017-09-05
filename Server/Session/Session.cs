using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Server
{
    enum SessionState
    {
        Start,
        UnAuthorized,
        Authorized,
        Closed,
    }

    // 表示一个会话，可能是TCP，也可能是UDP
    public interface Session
    {
        void Start();
        void Close();

        uint GetId();

        // 设置消息分发器
        void SetMessageDispatcher(IMessageDispatcher dispatcher);

        // 向对端发送buffer
        void SendMessage(byte[] buffer);
    }
}
