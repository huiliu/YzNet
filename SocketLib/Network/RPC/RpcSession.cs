using System;
using System.Diagnostics;

namespace Base.Network
{
    // RPC会话
    class RpcSession
    {
        public RpcSession(RpcManager mgr)
        {
            this.rpcMgr = mgr;
        }

        public void Close()
        {
            session.Close();
        }

        public void SendMessage(int MsgID, ByteBuffer data)
        {
            Debug.Assert(session != null, "RpcSession 错误！没有连接对象", "RPC");
            if (session != null)
            {
                session.SendMessage(MsgID, data);
            }
        }

        // 发送Rpc请求，不关注结果
        public void RpcPost(int MsgID, ByteBuffer data)
        {
            rpcMgr.RpcPost(this, MsgID, data);   
        }

        // 发送Rpc请求，同步等待结果
        public byte[] RpcSend(int MsgID, ByteBuffer data, int timeout = 1000 * 5)
        {
            return rpcMgr.RpcSend(this, MsgID, data, timeout);
        }

        // 发送Rpc请求，异步处理结果
        public void RpcSendAsync(int MsgID, ByteBuffer data, RpcResponseCallback cb, int timeout = 1000 * 5)
        {
            rpcMgr.RpcSendAsync(this, MsgID, data, cb, timeout);
        }

        private RpcManager rpcMgr;
        private INetSession session;
    }
}
