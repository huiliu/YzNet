using System;
using System.Diagnostics;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Server
{
    // 消息分发器
    public abstract class IMessageDispatcher
    {
        // 处理收到的消息
        public abstract void OnMessageReceived(Session session, byte[] data);

        // 处理断开的事件
        public abstract void OnDisconnected(Session session);

        // 注册关注的消息
        public void Register(uint msgNo, Action<Session, byte[]> action)
        {
            Debug.Assert(!processors.ContainsKey(msgNo), string.Format("消息ID: [{0}]已经注册了处理器[{1}", msgNo, processors[msgNo]), this.ToString());
            processors.Add(msgNo, action);
        }

        // 注销消息
        public void UnRegister(uint msgNo)
        {
            Debug.Assert(processors.ContainsKey(msgNo), string.Format("消息ID: [{0}]没有被注册", msgNo), this.ToString());
            processors.Remove(msgNo);
        }

        private Dictionary<uint, Action<Session, byte[]>> processors = new Dictionary<uint, Action<Session, byte[]>>();
    }
}
