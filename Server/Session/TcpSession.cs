using System;
using System.Diagnostics;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace Server
{
    // TCP会话
    // 表示一条TCP连接
    public class TcpSession : Session, IDisposable
    {
        public static TcpSession Create(Socket s)
        {
            var session = new TcpSession(s);
            return session;
        }

        public TcpSession(Socket socket)
        {
            this.id       = TcpSessionMgr.Instance.getSessionId();
            this.socket   = socket;

            // 设置socket参数
            socket.Blocking          = false;
            socket.NoDelay           = true;
            socket.SendBufferSize    = 1;
            socket.ReceiveBufferSize = 1;

            recvSAEA      = new SocketAsyncEventArgs();
            recvBuffer = new ByteBuffer(MessageHeader.MessageMaxLength);

            isSending     = false;
            sendSAEA      = new SocketAsyncEventArgs();
            toBeSendQueue = new Queue<ArraySegment<byte>>();

            recvSAEA.Completed  += recvSAEACompleted;
            sendSAEA.Completed  += sendSAEACompleted;

            statistics = new NetStatistics(this);
        }

        // 关闭Session
        public override void Close()
        {
            statistics.Close();

            CanReceive = false;
            IsConnected = false;
            socket.Close();
            dispatcher.OnDisconnected(this);
        }

        public void Dispose()
        {
            socket.Dispose();
            toBeSendQueue.Clear();
        }

        public override uint GetId()
        {
            return id;
        }

        private void shouldBeClose(Exception e)
        {
            Console.WriteLine("[Id: {2}]捕捉到异常!\nMessage: {0}\nStackTrace: {1}", e.Message, e.StackTrace, GetId());
            Close();
        }

        #region 接收网络消息
        // 开始接收网络消息
        protected override void startReceive()
        {
            if (recvBuffer.WriteableBytes == 0)
            {
                // 如果经常进入此块，应该初始时即将recvBuffer设置足够大
                Debug.WriteLine("没有足够的缓冲区接收数据！", this.ToString());
                recvBuffer.Shrink(1024);
            }

            recvSAEA.SocketFlags = SocketFlags.None;
            recvSAEA.SetBuffer(recvBuffer.Buffer, recvBuffer.WriteIndex, recvBuffer.WriteableBytes);

            try
            {
                if (!socket.ReceiveAsync(recvSAEA))
                {
                    // 同步完成
                    recvSAEACompleted(null, recvSAEA);
                }
            }
            catch (Exception e)
            {
                shouldBeClose(e);
            }
        }

        private void recvSAEACompleted(object sender, SocketAsyncEventArgs e)
        {
            if (!IsConnected || !CanReceive)
            {
                // Session状态不满足
                return;
            }

            if (e.BytesTransferred <= 0 || e.SocketError != SocketError.Success)
            {
                // 发生错误
                shouldBeClose(new InvalidOperationException());
                return;
            }

            statistics.TotalRecvBytes += e.BytesTransferred;

            // 移动游标
            recvBuffer.MoveWriteIndex(e.BytesTransferred);

            // 尝试解析并分发消息
            byte[] msg = null;
            while ((msg = MessageHeader.TryDecode(recvBuffer)) != null)
            {
                ++statistics.RecvPacketCount;
                dispatcher.OnMessageReceived(this, msg);
            }

            // 尝试调整Buffer
            recvBuffer.TryDefragment();

            // 再次开始接收
            startReceive();
        }
        #endregion

        #region 发送网络消息
        // 发送Buffer
        public override void SendMessage(byte[] data)
        {
            sendMessageImpl(data);
        }

        // 发送Buffer
        private void sendMessageImpl(byte[] data)
        {
            ++statistics.SendPacketCount;
            // 添加消息头
            var buff = MessageHeader.Encoding(data);

            lock(toBeSendQueue)
            {
                if (isSending)
                {
                    ++statistics.SendByQueue;

                    // 正在发送中，写入发送队列
                    toBeSendQueue.Enqueue(new ArraySegment<byte>(buff));
                    return;
                }

                isSending = true;
            }

            // 直接发送
            var sendQueue = new SendingQueue(buff);
            sendToSocket(sendQueue);
       }

        // 通过socket发送消息
        private void sendToSocket(SendingQueue queue)
        {
            try
            {
                sendSAEA.UserToken = queue;
                sendSAEA.SetBuffer(null, 0, 0);
                sendSAEA.BufferList = queue;

                ++statistics.CallSendAsyncCount;
                var ret = socket.SendAsync(sendSAEA);
                if (!ret)
                {
                    // 同步完成
                    sendSAEACompleted(null, sendSAEA);
                }
            }
            catch (Exception e)
            {
                shouldBeClose(e);
            }
        }

        // SendAsync回调
        private void sendSAEACompleted(object sender, SocketAsyncEventArgs e)
        {
            SocketError socketError;
            SendingQueue srcQueue;

            socketError = e.SocketError;
            srcQueue = e.UserToken as SendingQueue;

            if (!IsConnected)
            {
                return;
            }

            if (e.SocketError != SocketError.Success)
            {
                Close();
                return;
            }

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
                sendToSocket(new SendingQueue(arr));
            }
            else
            {
                // 还有部分数据没有发送
                sendToSocket(srcQueue);
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

        private uint    id;
        private Socket  socket;

        ByteBuffer                      recvBuffer;
        private SocketAsyncEventArgs    recvSAEA;

        private bool                        isSending;
        private SocketAsyncEventArgs        sendSAEA;
        private Queue<ArraySegment<byte>>   toBeSendQueue;

        private NetStatistics statistics;
    }
}
