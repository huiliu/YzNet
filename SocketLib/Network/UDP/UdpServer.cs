using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Threading;

namespace Base.Network
{
    // 集成KCP功能的UDP客户端
    public class UdpServer : INetSession, IDisposable
    {
        #region 网络事件
        public event Action<INetSession, int, byte[]> OnMessageReceived;  // 收到数据回调
        public event Action<INetSession>              OnDisconnected;     // 连接断开回调
        #endregion

        public UdpServer(Socket s, uint conv) : base()
        {
            Debug.Assert(s != null, "UDP会话有问题！无效socket.", "UdpSession");

            // socket
            socket = s;
            socket.SendBufferSize    = NetworkCommon.UdpSendBuffer;
            socket.ReceiveBufferSize = NetworkCommon.UdpRecvBuffer;

            clientEndPoint = null;

            // 接收数据相关
            recvData = new byte[NetworkCommon.UdpRecvBuffer];
            recvBuffer = new ByteBuffer(NetworkCommon.MaxPackageSize);
            recvSAEA = new SocketAsyncEventArgs();
            recvSAEA.Completed += onRecvCompleted;

            // 发送数据相关
            isSending = false;
            sendSAEA = new SocketAsyncEventArgs();
            sendSAEA.Completed += onSendCompleted;
            toBeSendingQueue = new Queue<byte[]>();

            this.conv = conv;
            // KCP相关参数
            kcp = new KCP(conv, kcpOut);
            lock(kcp)
            {
                kcp.NoDelay(1, 10, 2, 1);
                kcp.WndSize(NetworkCommon.KcpSendWnd, NetworkCommon.KcpRecvWnd);
            }

            isClosed = false;
        }

        public void Dispose()
        {
            recvSAEA.Dispose();
            sendSAEA.Dispose();
        }

        public override void Close()
        {
            isClosed = true;

            recvSAEA.Completed -= onRecvCompleted;
            sendSAEA.Completed -= onSendCompleted;

            socket.Close();
            recvBuffer.RetrieveAll();
            toBeSendingQueue.Clear();

            if (OnDisconnected != null)
            {
                OnDisconnected.Invoke(this);
            }
        }

        public uint GetKcpID()
        {
            return conv;
        }

        public void Update()
        {
            if (!isClosed)
            {
                kcpUpdate(Utils.IClock());
            }
        }

        #region 接收消息
        // 开始接收网络消息
        public override void StartReceive()
        {
            recvSAEA.RemoteEndPoint = new IPEndPoint(IPAddress.Any, 0);
            recvSAEA.SetBuffer(recvData, 0, recvData.Length);

            try
            {
                if (!socket.ReceiveFromAsync(recvSAEA))
                {
                    onRecvCompleted(this, recvSAEA);
                }
            }
            catch (Exception e)
            {
                handleException(e);
            }
        }

        // 异步接收消息返回
        private void onRecvCompleted(object sender, SocketAsyncEventArgs e)
        {
            if (isClosed)
            {
                return;
            }

            if (e.SocketError != SocketError.Success)
            {
                handleException(new InvalidOperationException(string.Format("Udp接收返回错错误[SocketError: {0}]", e.SocketError)));
                return;
            }

            if (e.BytesTransferred == 0)
            {
                Utils.logger.Warn(string.Format("[token: {0}]收到来自[{1}]长度为0的包", GetKcpID(), e.RemoteEndPoint), "UdpServer");
                if (zeroByteCount++ < maxZeroByteMsg)
                {
                    StartReceive();
                }
                else
                {
                    Utils.logger.Error(string.Format("[token: {0}]连接收到多次0长度包！", GetKcpID()), "UdpServer");
                    Close();
                }

                return;
            }

            zeroByteCount = 0;

            try
            {
                byte[] data = new byte[e.BytesTransferred];
                Array.Copy(e.Buffer, 0, data, 0, e.BytesTransferred);

                if (clientEndPoint == null)
                {
                    // 首次收到消息
                    // 记录客户端EndPoint
                    onNewConnection(e.RemoteEndPoint);
                }

                // 处理收到的消息
                onReceiveMessage(data);
            }
            catch(Exception err)
            {
                handleException(err);
            }
            finally
            {
                // 继续收取
                StartReceive();
            }
        }

        // 首次收到对端消息
        private void onNewConnection(EndPoint remoteEndPoint)
        {
            clientEndPoint = remoteEndPoint;
        }

        // "处理"收到的网络消息
        private void onReceiveMessage(byte[] buff)
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

        // 检查KCP是否有消息输出
        private void checkKcpReceiveMessage()
        {
            lock(kcp)
            {
                // 读取KCP中的消息
                for (var sz = kcp.PeekSize(); sz > 0; sz = kcp.PeekSize())
                {
                    if (OnMessageReceived != null)
                    {
                        byte[] b = new byte[sz];
                        if (kcp.Recv(b) > 0)
                        {
                            // 将消息分发
                            processReceivedKcpData(b);
                        }
                    }
                }
            }
        }

        // 处理来自KCP的数据
        private void processReceivedKcpData(byte[] data)
        {
            recvBuffer.WriteBytes(data);

            int msgID = -1;
            int cookie;
            byte[] msg = null;

            try
            {
                // 尝试解码消息
                while ((msg = MessageHeader.TryDecode(recvBuffer, out msgID, out cookie)) != null)
                {
                    // 将消息分发
                    OnMessageReceived?.Invoke(this, msgID, msg);
                }
            }
            catch (Exception e)
            {
                Utils.logger.Error(string.Format("处理消息[{0}]出错！Message: {1}\nStackTrace: {2}", msgID, e.Message, e.StackTrace), ToString());
            }

            recvBuffer.TryDefragment();
        }
        #endregion

        #region 发送消息
        // 发送消息
        public override void SendMessage(int msgID, ByteBuffer buff)
        {
            if (isClosed)
            {
                return;
            }

            // 添加消息头
            var msg = MessageHeader.Encoding(msgID, buff);

            lock(kcp)
            {
                // TODO: 需要调优
                if (kcp.WaitSnd() >= NetworkCommon.KcpSendWnd)
                {
                    // 累积太多KCP数据没有发送，也可以调高接收/发送窗口
                    Utils.logger.Warn(string.Format("{0}的KCP发送队列累积太多数据，关闭连接", GetKcpID()), "KCP");
                    Close();
                    return;
                }

                // TODO: 移除，调试
                if (kcp.WaitSnd() > 10)
                {
                    Utils.logger.Info(string.Format("UdpSession[{0}]当前发送队列长度为：[{1}]", GetKcpID(), kcp.WaitSnd()), "UdpServer");
                }

                // 交给KCP
                int ret = kcp.Send(msg.ReadAll());
                Debug.Assert(ret == 0, "Send Data into KCP Failed", this.ToString());

                kcp.Flush();
            }
        }

        // 发送KCP数据
        private void kcpOut(byte[] data, int size)
        {
            try
            {
                byte[] b = new byte[size];
                Array.Copy(data, 0, b, 0, size);

                // 将KCP消息发送给服务器
                sendToPeer(b);
            }
            catch (Exception e)
            {
                handleException(e);
            }
        }

        // 发送Buffer至网络
        private void sendToPeer(byte[] data)
        {
            lock(toBeSendingQueue)
            {
                if (isSending)
                {
                    // 正在发送中，写入发送队列
                    toBeSendingQueue.Enqueue(data);
                    if (toBeSendingQueue.Count >= NetworkCommon.MaxCacheMessage)
                    {
                        // 消息缓存数超过上限
                        Utils.logger.Error(string.Format("Session[{0}]消息缓存数超过上限！强制关闭连接", GetKcpID()), ToString());
                        Close();
                    }

                    return;
                }

                isSending = true;
            }

            // 直接发送
            sendToSocket(data);
       }

        // 通过socket发送消息
        private void sendToSocket(byte[] data)
        {
            if (socket == null || isClosed)
            {
                Close();
                return;
            }

            Debug.Assert(clientEndPoint != null, "没有对端地址！", "UdpSession");

            sendSAEA.RemoteEndPoint = clientEndPoint;
            sendSAEA.SetBuffer(data, 0, data.Length);

            sendToSocketEx(sendSAEA);

        }

        private void sendToSocketEx(SocketAsyncEventArgs e)
        {
            try
            {
                if (!socket.SendToAsync(e))
                {
                    // 同步完成
                    onSendCompleted(null, e);
                }
            }
            catch (Exception err)
            {
                handleException(err);
            }
        }

        // SendAsync回调
        private void onSendCompleted(object sender, SocketAsyncEventArgs e)
        {
            if (isClosed)
            {
                return;
            }

            if (e.SocketError != SocketError.Success)
            {
                Close();
                return;
            }

            if (e.BytesTransferred != e.Buffer.Length)
            {
                // 数据未发送完

                e.SetBuffer(e.Buffer, e.Offset, e.Buffer.Length - e.BytesTransferred);
                Utils.logger.Warn(string.Format("UdpServer数据没有一次发送完，继续发送!"), "UdpServer");
                sendToSocketEx(e);
            }
            else
            {
                // 全部发送完毕

                byte[] data = null;
                lock(toBeSendingQueue)
                {
                    int count = toBeSendingQueue.Count;
                    if (count == 0)
                    {
                        // 没有缓存数据
                        isSending = false;
                        return;
                    }

                    // 读出缓存数据
                    data = toBeSendingQueue.Dequeue();
                }

                if (data != null)
                {
                    // 发送缓存数据
                    sendToSocket(data);
                }
            }
        }
#endregion

        private void handleException(Exception e)
        {
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

        private bool   isClosed;
        private Socket socket;
        private EndPoint clientEndPoint;

        // TODO: 优化
        private int zeroByteCount = 0;
        private const int maxZeroByteMsg = 10;

        private byte[]               recvData;
        private ByteBuffer           recvBuffer;
        private SocketAsyncEventArgs recvSAEA;

        private bool isSending;
        private SocketAsyncEventArgs    sendSAEA;
        private Queue<byte[]>           toBeSendingQueue;

        #region KCP相关
        private uint    conv;
        private KCP     kcp;
        private UInt32  nextUpdateTimeMs;
        #endregion
    }
}
