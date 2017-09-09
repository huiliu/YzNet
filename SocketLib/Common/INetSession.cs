using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace YezhStudio.Base.Network
{
    // 表示一个会话，可能是TCP，也可能是UDP
    public abstract class INetSession
    {
        public static event Action<INetSession> OnSessionClosed;
        public static event Action<INetSession, byte[]> OnMessageReceived;

        public uint SessionID { get; set; }

        private bool isConnected;
        public bool IsConnected
        {
            get { return isConnected; }
            set { isConnected = value; }
        }

        // 关闭会话
        public virtual void Close()
        {
            triggerSessionClosed(this);
        }

        // Session关闭事件
        protected void triggerSessionClosed(INetSession session)
        {
            if (OnSessionClosed != null)
            {
                OnSessionClosed.Invoke(this);
            }
        }

        // 发到消息的事件
        protected void triggerMessageReceived(INetSession session, byte[] data)
        {
            if (OnMessageReceived != null)
            {
                OnMessageReceived.Invoke(session, data);
            }
        }

        // 向对端发送buffer
        public abstract void SendMessage(byte[] buffer);

        // 开始接收数据
        public abstract void startReceive();
    }
}
