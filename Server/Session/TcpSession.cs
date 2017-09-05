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
    public class TcpSession : Session
    {
        private static int MaxPackageSize = 1024 * 8;
        public TcpSession(uint id, Socket socket)
        {
            this.id       = id;
            this.socket   = socket;
            stream        = new NetworkStream(socket);
            sendSAEA      = new SocketAsyncEventArgs();
            isSending     = false;
            toBeSendQueue = new Queue<ArraySegment<byte>>();

            sendSAEA.Completed    += SendSAEACompleted;
        }

        // 启动会话
        // 开始接收网络消息
        public void Start()
        {
            Debug.Assert(stream.CanRead, "Socket没有连接！", "Session");

            socket.NoDelay           = true;
            socket.SendBufferSize    = 1;
            socket.ReceiveBufferSize = 1;

            state  = SessionState.Start;

            // 在一个独立的线程开始接收消息
            Task.Run(async () =>
            {
                while (state != SessionState.Closed)
                    await startReceive();
            });
        }

        // 关闭Session
        public void Close()
        {
            state = SessionState.Closed;
            TcpSessionMgr.Instance.HandleSessionClosed(this);

            socket.Close();
            dispatcher.OnDisconnected(this);
        }

        // 设置网络消息分发器
        public void SetMessageDispatcher(IMessageDispatcher dispatcher)
        {
            this.dispatcher = dispatcher;
        }

        #region 接收网络消息
        ByteBuffer receiveBuffer = new ByteBuffer();
        // 开始接收网络消息
        private async Task startReceive()
        {
            int receivedSize = 0;
            try
            {
                // 读取网络消息
                receivedSize = await stream.ReadAsync(receiveBuffer.Buffer, receiveBuffer.WriteIndex, receiveBuffer.WriteableBytes);
                if (receivedSize == 0)
                {
                    Close();
                    return;
                }
            }
            catch (Exception e)
            {
                shouldBeClose(e);
                return;
            }

            receiveBuffer.MoveWriteIndex(receivedSize);

            // 分发消息
            byte[] message = null;
            while ((message = MessageHeader.TryDecode(receiveBuffer)) != null)
            {
                // 消息应该放入主线程处理
                dispatcher.OnMessageReceived(this, message);
            }
        }
        #endregion

        #region 发送网络消息

        ByteBuffer toBeSending = new ByteBuffer();
        public void SendMessageEx(byte[] data)
        {
            lock(toBeSending)
            {
                if (isSending)
                {
                    toBeSending.WriteBytes(data);
                    return;
                }

                isSending = true;
            }

            sendToSocket(data);
        }

        private async void sendToSocket(byte[] data)
        {
            try
            {
                await stream.WriteAsync(data, 0, data.Length);
            }
            catch (Exception e)
            {
                shouldBeClose(e);
                return;
            }

            byte[] buff = null;
            lock(toBeSending)
            {
                if (toBeSending.ReadableBytes == 0)
                {
                    isSending = false;
                    return;
                }

                buff = toBeSending.ReadAll();
            }

            sendToSocket(buff);
        }

        // 发送Buffer
        public void SendMessage(byte[] data)
        {
            sendMessageImpl(data);
        }

        // 发送Buffer
        private void sendMessageImpl(byte[] data)
        {
            // 添加消息头
            var buff = MessageHeader.Encoding(data);

            lock(toBeSendQueue)
            {
                if (isSending)
                {
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

                var ret = socket.SendAsync(sendSAEA);
                if (!ret)
                {
                    // 同步完成
                    SendSAEACompleted(null, sendSAEA);
                }
            }
            catch (Exception e)
            {
                shouldBeClose(e);
            }
        }

        // SendAsync回调
        private void SendSAEACompleted(object sender, SocketAsyncEventArgs e)
        {
            SocketError socketError;
            SendingQueue srcQueue;

            socketError = e.SocketError;
            srcQueue = e.UserToken as SendingQueue;

            if (state != SessionState.Start)
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

        public uint GetId()
        {
            return id;
        }

        private void shouldBeClose(Exception e)
        {
            Console.WriteLine("[Id: {2}]捕捉到异常!\nMessage: {0}\nStackTrace: {1}", e.Message, e.StackTrace, GetId());
            Close();
        }

        private uint                id;
        private Socket              socket;
        private NetworkStream       stream;
        private SessionState        state;
        private IMessageDispatcher  dispatcher;

        private SocketAsyncEventArgs        sendSAEA;
        private bool                        isSending;
        private Queue<ArraySegment<byte>>   toBeSendQueue;
    }
}
