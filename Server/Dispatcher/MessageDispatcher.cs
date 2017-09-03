using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Server
{
    // 消息分发器
    public interface MessageDispatcher
    {
        void Start();
        void Stop();

        // 处理收到的消息
        Task OnMessageReceived(Session session, byte[] data, int offset, int count);

        // 处理断开的事件
        Task OnDisconnected(Session session);
    }
}
