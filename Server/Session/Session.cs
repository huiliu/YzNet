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
    public abstract class INetSession
    {
        private bool isConnected;
        public bool IsConnected
        {
            get { return isConnected; }
            set { isConnected = value; }
        }

        private bool canReceive;
        public bool CanReceive
        {
            get { return canReceive; }
            set
            {
                canReceive = value;
                if (IsConnected && canReceive)
                {
                    startReceive();
                }
            }
        }

        public abstract void Close();

        public abstract uint GetId();

        // 设置消息分发器
        protected IMessageDispatcher dispatcher;
        public void SetMessageDispatcher(IMessageDispatcher _dispatcher)
        {
            dispatcher = _dispatcher;
        }

        // 向对端发送buffer
        public abstract void SendMessage(byte[] buffer);

        protected abstract void startReceive();
    }
}
