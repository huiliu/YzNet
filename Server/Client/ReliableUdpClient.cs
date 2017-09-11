using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace Server
{
    // 集成KCP功能的UDP客户端
    public class ReliableUdpClient : IDisposable
    {
        public event Action<ReliableUdpClient>                     OnConnected;        // 连接建立成功回调
        public event Action<ReliableUdpClient, byte[], int, int>   OnMessageReceived;  // 收到数据回调
        public event Action<ReliableUdpClient>                     OnDisconnected;     // 连接断开回调

        public ReliableUdpClient(UInt32 conv)
        {
            this.conv = conv;
            isConnected = false;

            recvBuffer = new byte[NetworkCommon.UdpRecvBuffer];
            recvSAEA = new SocketAsyncEventArgs();
            recvSAEA.Completed += onRecvCompleted;

            isSending = false;
            sendSAEA = new SocketAsyncEventArgs();
            sendSAEA.Completed += onSendCompleted;
            toBeSendQueue = new Queue<ArraySegment<byte>>();

            kcp = new KCP(conv, kcpOut);
            lock(kcp)
            {
                kcp.NoDelay(1, 10, 2, 1);
                kcp.WndSize(NetworkCommon.KcpSendWnd, NetworkCommon.KcpRecvWnd);
            }

            statistics = new NetStatistics(null);
        }

        // 连接服务器
        public void Connect(string host, int port)
        {
            try
            {
                // 连接UDP服务器
                socket = new Socket(SocketType.Dgram, ProtocolType.Udp);
                socket.Connect(IPAddress.Parse(host), port);

                socket.SendBufferSize    = NetworkCommon.UdpSendBuffer;
                socket.ReceiveBufferSize = NetworkCommon.UdpRecvBuffer;

                isConnected = true;

                // 开始接收网络数据
                startReceive();

                // TODO: 优化
                Task.Run(() =>
                {
                    while (isConnected)
                    {
                        kcpUpdate(Utils.IClock());
                        Thread.Sleep(5);
                    }
                });
            }
            catch (Exception e)
            {
                shouldBeClose(e);
            }
        }

        public void Dispose()
        {
            recvSAEA.Dispose();
            sendSAEA.Dispose();
            
            toBeSendQueue.Clear();
            socket.Dispose();
        }

        public void Close()
        {
            recvSAEA.Completed -= onRecvCompleted;
            sendSAEA.Completed -= onSendCompleted;

            isConnected = false;
            socket.Close();
            OnDisconnected?.Invoke(this);
        }

        public uint GetID()
        {
            return conv;
        }

        #region 接收消息
        private void startReceive()
        {
            recvSAEA.SetBuffer(recvBuffer, 0, recvBuffer.Length);

            try
            {
                if (!socket.ReceiveAsync(recvSAEA))
                {
                    onRecvCompleted(this, recvSAEA);
                }
            }
            catch (Exception e)
            {
                shouldBeClose(e);
            }
        }

        private void onRecvCompleted(object sender, SocketAsyncEventArgs e)
        {
            if (!isConnected)
            {
                return;
            }

            if (e.BytesTransferred <= 0 || e.SocketError != SocketError.Success)
            {
                shouldBeClose(new InvalidOperationException("Udp接收返回0或出错"));
                return;
            }

            // TODO: 优化
            byte[] recvData = new byte[e.BytesTransferred];
            Array.Copy(e.Buffer, e.Offset, recvData, 0, e.BytesTransferred);
            OnReceiveMessage(recvData);

            // 继续收取
            startReceive();
        }

        // "处理"收到的网络消息
        private void OnReceiveMessage(byte[] buff)
        {
            lock(kcp)
            {
                // 交给KCP处理
                var ret = kcp.Input(buff);
                Debug.Assert(ret == 0, "KCP INPUT数据出错！", "UDP");

                kcp.Flush();
            }

            checkKcpReceiveMessage();
        }

        private void checkKcpReceiveMessage()
        {
            try
            {
                lock(kcp)
                {
                    // 读取KCP中的消息
                    for (var sz = kcp.PeekSize(); sz > 0; sz = kcp.PeekSize())
                    {
                        byte[] b = new byte[sz];
                        if (kcp.Recv(b) > 0)
                        {
                            // 将消息分发
                            OnMessageReceived?.Invoke(this, b, 0, sz);
                        }
                    }
                }
            }
            catch (Exception e)
            {
                shouldBeClose(e);
            }
        }
        #endregion

        #region 发送消息
        // 发送消息
        public void SendMessage(byte[] buff)
        {
            // 添加消息头
            // var data = MessageHeader.Encoding(buff);

            lock(kcp)
            {
                // TODO: 需要调优
                if (kcp.WaitSnd() == NetworkCommon.KcpSendWnd)
                {
                    // 累积太多KCP数据没有发送，也可以调高接收/发送窗口
                    Close();
                    return;
                }

                // 交给KCP
                int ret = kcp.Send(buff);
                Debug.Assert(ret == 0, "Send Data into KCP Failed", this.ToString());

                kcp.Flush();
            }
        }

        // 发送KCP数据
        private void kcpOut(byte[] data, int size)
        {
            try
            {
                // 将KCP消息发送给服务器
                byte[] b = new byte[size];
                Array.Copy(data, 0, b, 0, size);
                sendMessageImpl(b);
            }
            catch (Exception e)
            {
                shouldBeClose(e);
            }
        }

        // 发送Buffer
        private void sendMessageImpl(byte[] data)
        {
            ++statistics.SendPacketCount;

            lock(toBeSendQueue)
            {
                if (isSending)
                {
                    ++statistics.SendByQueue;

                    // 正在发送中，写入发送队列
                    toBeSendQueue.Enqueue(new ArraySegment<byte>(data));
                    if (toBeSendQueue.Count >= NetworkCommon.MaxCacheMessage)
                    {
                        // 消息缓存数超过上限
                        Console.Write(string.Format("Session[{0}]消息缓存数超过上限！强制关闭连接", GetID()), ToString());
                        Close();
                    }

                    return;
                }

                isSending = true;
            }

            // 直接发送
            var sendQueue = new SendingQueue(data);
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
                    onSendCompleted(null, sendSAEA);
                }
            }
            catch (Exception e)
            {
                shouldBeClose(e);
            }
        }

        // SendAsync回调
        private void onSendCompleted(object sender, SocketAsyncEventArgs e)
        {
            SocketError socketError;
            SendingQueue srcQueue;

            socketError = e.SocketError;
            srcQueue = e.UserToken as SendingQueue;

            if (!isConnected)
            {
                return;
            }

            if (e.SocketError != SocketError.Success)
            {
                Console.WriteLine("Udp发送出错!");
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

        private void shouldBeClose(Exception e)
        {
            Console.WriteLine(string.Format("发生错误！Message: {0}\nStackTrace: {1}", e.Message, e.StackTrace), "UdpCLient");
            Close();
        }

        // KCP定时Update
        private void kcpUpdate(UInt32 currentMs)
        {
            if (currentMs >= nextUpdateTimeMs)
            {
                lock(kcp)
                {
                    kcp.Update(currentMs);

                    nextUpdateTimeMs = kcp.Check(currentMs);
                }

                checkKcpReceiveMessage();
                //Console.WriteLine("Kcp Update: {0}, NextTime: {1}", Utils.IClock(), nextUpdateTimeMs);
            }
        }

        private bool   isConnected;
        private Socket socket;

        private byte[]               recvBuffer;
        private SocketAsyncEventArgs recvSAEA;

        private bool isSending;
        private SocketAsyncEventArgs        sendSAEA;
        private Queue<ArraySegment<byte>>   toBeSendQueue;

        #region KCP相关
        private uint    conv;
        private KCP     kcp;
        private UInt32  nextUpdateTimeMs;
        #endregion

        private NetStatistics statistics;
    }
}