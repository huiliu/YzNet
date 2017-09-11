using System;
using System.Diagnostics;

namespace YezhStudio.Base.Network
{
    // RPC会话
    class RpcSession
    {
        public RpcSession(RpcManager mgr)
        {

        }

        public void Close()
        {
            session.Close();
        }

        public void SendMessage(byte[] data)
        {
            Debug.Assert(session != null, "RpcSession 错误！没有连接对象", "RPC");
            if (session != null)
            {
                session.SendMessage(data);
            }
        }

        // 发送Rpc请求，不关注结果
        public void RpcPost(byte[] data)
        {
            mgr.RpcPost(this, data);   
        }

        // 发送Rpc请求，同步等待结果
        public byte[] RpcSend(byte[] data, int timeout = 1000 * 5)
        {
            return mgr.RpcSend(this, data, timeout);
        }

        // 发送Rpc请求，异步处理结果
        public void RpcSendAsync(byte[] data, RpcResponseCallback cb, int timeout = 1000 * 5)
        {
            mgr.RpcSendAsync(this, data, cb, timeout);
        }

        private RpcManager mgr;
        private INetSession session;
    }
}
