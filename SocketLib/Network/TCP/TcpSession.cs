using System;
using System.Diagnostics;
using System.Collections.Generic;
using System.Net.Sockets;

namespace Base.Network
{
    // TCP会话
    // 表示一条TCP连接
    public class TcpSession : INetSession, IDisposable
    {
        public static event Action<INetSession>                 OnSessionClosed;
        public static event Action<INetSession, int, byte[]>    OnMessageReceived;

        public TcpSession(Socket socket)
        {
            this.socket = socket;

            // 设置socket参数
            socket.NoDelay           = true;
            socket.SendBufferSize    = NetworkCommon.TcpSendBuffer;
            socket.ReceiveBufferSize = NetworkCommon.TcpRecvBuffer;

            recvSAEA      = new SocketAsyncEventArgs();
            recvSAEA.Completed  += RecvSAEACompleted;
            recvBuffer    = new ByteBuffer(NetworkCommon.MaxPackageSize + 100);

            isSending     = false;
            sendSAEA      = new SocketAsyncEventArgs();
            sendSAEA.Completed  += SendSAEACompleted;
            toBeSendQueue = new Queue<ArraySegment<byte>>();

            isClosed = false;
            statistics = new NetStatistics(this);
        }

        // 关闭Session
        public override void Close()
        {
            isClosed = true;

            socket.Close();
            toBeSendQueue.Clear();
            statistics.Close();

            OnSessionClosed?.Invoke(this);
        }

        public void Dispose()
        {
            socket.Dispose();
            recvSAEA.Dispose();
            sendSAEA.Dispose();
        }

        private void HandleException(Exception e)
        {
            Utils.logger.Info("[Id: {2}]捕捉到异常!\nMessage: {0}\nStackTrace: {1}", e.Message, e.StackTrace, SessionID);
            Close();
        }

        #region 接收网络消息
        // 开始接收网络消息
        public override void StartReceive()
        {
            if (recvBuffer.WriteableBytes == 0)
            {
                // 如果经常进入此块，应该初始时即将recvBuffer设置足够大
                Utils.logger.Warn("没有足够的缓冲区接收数据！", "TcpSession");
                recvBuffer.Shrink(1024);
            }

            recvSAEA.SocketFlags = SocketFlags.None;
            recvSAEA.SetBuffer(recvBuffer.Buffer, recvBuffer.WriteIndex, recvBuffer.WriteableBytes);

            try
            {
                if (!socket.ReceiveAsync(recvSAEA))
                {
                    // 同步完成
                    RecvSAEACompleted(null, recvSAEA);
                }
            }
            catch (Exception e)
            {
                HandleException(e);
            }
        }

        private void RecvSAEACompleted(object sender, SocketAsyncEventArgs e)
        {
            if (isClosed)
            {
                // Session状态不满足
                Utils.logger.Error("会话状态错误！", "TcpSession");
                return;
            }

            if (e.BytesTransferred <= 0 || e.SocketError != SocketError.Success)
            {
                // 发生错误
                HandleException(new InvalidOperationException("接收操作失败或对方关闭了连接！"));
                return;
            }

            statistics.TotalRecvBytes += e.BytesTransferred;

            // 移动游标
            recvBuffer.MoveWriteIndex(e.BytesTransferred);

            // 尝试解析并分发消息
            int msgID = -1;
            byte[] msg = null;

            try
            {
                while ((msg = MessageHeader.TryDecode(recvBuffer, out msgID, out int cookie)) != null)
                {
                    ++statistics.RecvPacketCount;
                    OnMessageReceived?.Invoke(this, msgID, msg);
                }
            }
            catch (Exception err)
            {
                Utils.logger.Error(string.Format("处理消息[{0}]出错！Message: {1}\nStackTrace: {2}", msgID, err.Message, err.StackTrace), "TcpSession");
            }

            // 尝试调整Buffer
            recvBuffer.TryDefragment();

            // 再次开始接收
            StartReceive();
        }
        #endregion

        #region 发送网络消息
        // 发送Buffer
        public override void SendMessage(int msgID, ByteBuffer data)
        {
            if (!isClosed)
            {
                SendMessageImpl(msgID, data);
            }
            else
            {
                Utils.logger.Error(string.Format("Socket已经关闭!"), "TcpSession");
            }
        }

        // 发送Buffer
        private void SendMessageImpl(int msgID, ByteBuffer data)
        {
            ++statistics.SendPacketCount;
            // 添加消息头
            var buff = MessageHeader.Encoding(msgID, data);

            lock(toBeSendQueue)
            {
                if (isSending)
                {
                    ++statistics.SendByQueue;

                    // 正在发送中，写入发送队列
                    toBeSendQueue.Enqueue(new ArraySegment<byte>(buff.ReadAll()));
                    if (toBeSendQueue.Count >= NetworkCommon.MaxCacheMessage)
                    {
                        // 消息缓存数超过上限
                        Utils.logger.Error(string.Format("Session[{0}]消息缓存数超过上限！强制关闭连接", SessionID), "TcpSession");
                        Close();
                    }

                    Console.WriteLine(string.Format("TcpServer[{0}]当前发送队列长度为：{1}", SessionID, toBeSendQueue.Count), "TcpSession");
                    Utils.logger.Error(string.Format("TcpServer[{0}]当前发送队列长度为：{1}", SessionID, toBeSendQueue.Count), "TcpSession");
                    return;
                }

                isSending = true;
            }

            // 直接发送
            var sendQueue = new SendingQueue(buff.ReadAll());
            SendToSocket(sendQueue);
       }

        // 通过socket发送消息
        private void SendToSocket(SendingQueue queue)
        {
            try
            {
                sendSAEA.UserToken = queue;
                sendSAEA.SetBuffer(null, 0, 0);
                sendSAEA.BufferList = queue;

                ++statistics.CallSendAsyncCount;
                if (!socket.SendAsync(sendSAEA))
                {
                    // 同步完成
                    SendSAEACompleted(null, sendSAEA);
                }
            }
            catch (Exception e)
            {
                HandleException(e);
            }
        }

        // SendAsync回调
        private void SendSAEACompleted(object sender, SocketAsyncEventArgs e)
        {
            if (isClosed)
            {
                return;
            }

            if (e.SocketError != SocketError.Success)
            {
                HandleException(new Exception(string.Format("发送返回错误[{0}]", e.SocketError)));
                return;
            }

            Console.WriteLine("TcpSession[{0}]发送返回[{1}]!", SessionID, e.BytesTransferred);
            SendingQueue srcQueue;
            srcQueue = e.UserToken as SendingQueue;

            if (srcQueue.Trim(e.BytesTransferred))
            {
                statistics.TotalSendBytes += e.BytesTransferred;
                // 全部发送完毕

                ArraySegment<byte>[] arr;
                lock(toBeSendQueue)
                {
                    int count = toBeSendQueue.Count;
                    if (count == 0)
                    {
                        // 没有缓存数据
                        isSending = false;
                        return;
                    }

                    // 读出缓存数据
                    arr = new ArraySegment<byte>[count];
                    for (int i = 0; i < count; ++i)
                    {
                        arr[i] = toBeSendQueue.Dequeue();
                    }
                }

                // 发送缓存数据
                SendToSocket(new SendingQueue(arr));
            }
            else
            {
                // 还有部分数据没有发送
                SendToSocket(srcQueue);
            }
        }

        // 正在发送的队列
        public sealed class SendingQueue : IList<ArraySegment<byte>>
        {
            private int count;
            private ArraySegment<byte>[] sendList;
            private int interOffset;

            public SendingQueue(ArraySegment<byte>[] sendList)
            {
                this.sendList = sendList;
                this.interOffset = 0;
                this.count = sendList.Length;
            }

            public SendingQueue(byte[] bs)
            {
                ArraySegment<byte>[] arr = new ArraySegment<byte>[1];
                arr[0] = new ArraySegment<byte>(bs);
                this.sendList = arr;
                this.interOffset = 0;
                this.count = 1;
            }

            public bool IsReadOnly
            {
                get
                {
                    return true;
                }
            }

            public int Count
            {
                get
                {
                    return count - interOffset;
                }
            }

            public int IndexOf(ArraySegment<byte> item)
            {
                throw new NotSupportedException();
            }

            public void Insert(int index, ArraySegment<byte> item)
            {
                throw new NotSupportedException();
            }

            public void RemoveAt(int index)
            {
                throw new NotSupportedException();
            }

            public ArraySegment<byte> this[int index]
            {
                get
                {
                    int _index = interOffset + index;
                    return sendList[_index];
                }
                set
                {
                    throw new NotSupportedException();
                }
            }

            public void Add(ArraySegment<byte> item)
            {
                throw new NotSupportedException();
            }

            public void Clear()
            {
                throw new NotSupportedException();
            }

            public bool Contains(ArraySegment<byte> item)
            {
                throw new NotSupportedException();
            }

            public void CopyTo(ArraySegment<byte>[] array, int arrayIndex)
            {
                for (int i = 0; i < Count; i++)
                {
                    array[arrayIndex + i] = this[i];
                }
            }

            public bool Remove(ArraySegment<byte> item)
            {
                throw new NotSupportedException();
            }

            public IEnumerator<ArraySegment<byte>> GetEnumerator()
            {
                for (int i = 0; i < Count; i++)
                {
                    yield return sendList[interOffset + i];
                }
            }

            System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
            {
                return GetEnumerator();
            }

            // 剔除掉已发送数据长度
            public bool Trim(int sentLen)
            {
                int total = 0;
                int count = Count;
                for (int i = 0; i < count; i++)
                {
                    var segment = sendList[interOffset];
                    total += segment.Count;
                    if (total <= sentLen)
                    {
                        ++interOffset;
                        continue;
                    }
                    int rest = total - sentLen;
                    sendList[interOffset] = new ArraySegment<byte>(segment.Array, segment.Offset + segment.Count - rest, rest);
                    return false;
                }
                // 全部发送完毕
                return true;
            }
        }
        #endregion

        private Socket  socket;
        private bool isClosed;
        ByteBuffer                      recvBuffer;
        private SocketAsyncEventArgs    recvSAEA;

        private bool                        isSending;
        private SocketAsyncEventArgs        sendSAEA;
        private Queue<ArraySegment<byte>>   toBeSendQueue;

        private NetStatistics statistics;
    }
}
