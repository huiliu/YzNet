using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;

namespace YezhStudio.Base.Network
{
    public delegate void RpcResponseCallback(bool result, byte[] data);

    class RpcRequestInfo
    {
        public int  RequestID;
        public long DeadTime;
        public RpcResponseCallback Callback;
    }

    // RPC服务
    // 负责发送RPC请求，维护RPC请求队列，处理RPC响应
    class RpcManager
    {

        public RpcManager()
        {
            cookie = 0;
            reqDict = new Dictionary<int, RpcRequestInfo>();
        }

        // 发送Rpc请求，不关注结果
        public void RpcPost(RpcSession session, int MsgID, ByteBuffer data)
        {
            session.SendMessage(MsgID, data);
        }

        // 发送Rpc请求，同步等待结果
        public byte[] RpcSend(RpcSession session, int MsgID, ByteBuffer data, int timeout)
        {
            var ev = new AutoResetEvent(false);
            byte[] ret = null;

            RpcSendAsync(session, MsgID, data, delegate (bool result, byte[] response)
            {
                ret = response;
                ev.Set();
            }, timeout);

            ev.WaitOne();

            return ret;
        }

        // 发送Rpc请求，异步处理结果
        public void RpcSendAsync(RpcSession session, int MsgID, ByteBuffer data, RpcResponseCallback cb, int timeout)
        {
            var request = new RpcRequestInfo();
            request.RequestID = getCookie();
            request.DeadTime = Utils.IClock() + timeout;
            request.Callback = cb;

            lock(reqDict)
            {
                reqDict[request.RequestID] = request;
            }

            session.SendMessage(MsgID, data);
        }

        // 处理收到的RPC结果
        private void processRpcResponse(byte[] data)
        {
            var rpcResponse = RpcResponse.Decode(data);
            RpcRequestInfo reqInfo = null;
            lock(reqDict)
            {
                if (reqDict.ContainsKey(rpcResponse.RequestID))
                {
                    reqInfo = reqDict[rpcResponse.RequestID];
                    reqDict.Remove(rpcResponse.RequestID);
                }
                else
                {
                    Utils.logger.Warn(string.Format("RPC请求已经超时或不存在的RPC请求！[requestID: {0}]", rpcResponse.RequestID), "RPC");
                }
            }

            if (reqInfo != null)
            {
                Debug.Assert(rpcResponse.RequestID == reqInfo.RequestID, "RPC错误", "RPC");
                reqInfo.Callback.Invoke(true, data);
            }
        }

        // 定时检查超时的RPC请求
        private void scanTimeoutResponse()
        {
            try
            {
                long now = Utils.IClock();
                List<RpcRequestInfo> timeoutList = null;
                lock (reqDict)
                {
                    foreach (KeyValuePair<int, RpcRequestInfo> kv in reqDict)
                    {
                        var reqInfo = kv.Value;
                        if (reqInfo != null && reqInfo.DeadTime <= now)
                        {
                            // RPC响应超时
                            if (timeoutList == null)
                            {
                                timeoutList = new List<RpcRequestInfo>();
                            }

                            timeoutList.Add(reqInfo);
                            reqDict.Remove(kv.Key);
                        }
                    }
                }

                timeoutList.ForEach(req =>
                {
                    req.Callback(false, null);
                });

                timeoutList.Clear();
            }
            catch (Exception e)
            {
                Utils.logger.Error(string.Format("RPC扫描处理超时请求时出错！\nMessage: {0}\nStackTrace: {1}", e.Message, e.StackTrace), "RPC");
            }
            finally
            {
                // 重新创建Timer
            }
        }

        // 取得一个唯一的RPC请求cookie
        private int getCookie()
        {
            Interlocked.CompareExchange(ref cookie, 0, int.MaxValue);
            return Interlocked.Add(ref cookie, 1);
        }

        private int cookie;
        private Dictionary<int, RpcRequestInfo> reqDict;
    }
}
