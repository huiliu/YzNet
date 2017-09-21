using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;

namespace Base.Network
{
    enum ServerState
    {
        Start,
        Closed,
    }
    // UDP服务器，侦听特定端口，接受网络数据交给UdpSession
    public class UdpServer : IDisposable
    {
        // 服务关闭
        public event Action                                     OnServerClose;

        // 收到UDP数据
        // 需要处理可能是首次连接的客户端
        public event Action<UdpServer, EndPoint, byte[], int>   OnMessageReceived;

        public UdpServer()
        {
            isSending = false;
            toBeSendingQueue = new Queue<DatagramPacket>();
            sendSAEA = new SocketAsyncEventArgs();
            sendSAEA.Completed += OnSendCompleted;

            // TODO: 优化
            recvBuffer = new byte[NetworkCommon.UdpRecvBuffer];
            recvSAEA = new SocketAsyncEventArgs();
            recvSAEA.Completed += OnRecvCompleted;
        }

        // 启动UDP服务，开始接受"新连接"和数据
        public void StartServiceOn(string ip, int port)
        {
            if (socket != null)
            {
                Debug.Assert(false, "Server已经启动!", "Server");
                return;
            }

            // 侦听Udp端口
            IPAddress address = IPAddress.Parse(ip);
            socket = new Socket(address.AddressFamily, SocketType.Dgram, ProtocolType.Udp);
            socket.Bind(new IPEndPoint(address, port));

            socket.SendBufferSize    = NetworkCommon.UdpSendBuffer;
            socket.ReceiveBufferSize = NetworkCommon.UdpRecvBuffer;

            state = ServerState.Start;
            StartReceive();
        }

        // 停止服务
        public void Stop()
        {
            state = ServerState.Closed;

            recvSAEA.Completed -= OnRecvCompleted;
            sendSAEA.Completed -= OnSendCompleted;

            toBeSendingQueue.Clear();

            socket.Close();
            OnServerClose?.Invoke();
        }

        public void Dispose()
        {
            socket.Dispose();
            sendSAEA.Dispose();
            recvSAEA.Dispose();
        }

        #region 接收数据
        // 开始接收数据
        private void StartReceive()
        {
            recvSAEA.RemoteEndPoint = new IPEndPoint(IPAddress.Any, 0);
            recvSAEA.SetBuffer(recvBuffer, 0, recvBuffer.Length);

            try
            {
                if (!socket.ReceiveFromAsync(recvSAEA))
                {
                    OnRecvCompleted(this, recvSAEA);
                }
            }
            catch (Exception e)
            {
                ShouldBeClose(e);
            }
        }

        private void OnRecvCompleted(object sender, SocketAsyncEventArgs e)
        {
            if (state != ServerState.Start)
            {
                return;
            }

            if (e.SocketError != SocketError.Success)
            {
                ShouldBeClose(new Exception("接收错误！"));
                return;
            }

            try
            {
                // 处理收到的网络消息，如果使用异步，需要将BUFFER拷贝一份
                OnMessageReceived(this, e.RemoteEndPoint, e.Buffer, e.BytesTransferred);
            }
            catch (Exception err)
            {
                Utils.logger.Error(string.Format("处理来自[{0}]的消息出错！\nMessage: {1}\nStackStrace: {2}", e.RemoteEndPoint, err.Message, err.StackTrace), "UdpServer");
            }
            finally
            {
                // 继续收取
                StartReceive();
            }
        }
        #endregion

        #region 发送数据
        // 发送消息至remoteEndPoint
        public void SendMessage(byte[] buff, object remoteEndPoint = null)
        {
            if (state != ServerState.Start)
            {
                return;
            }

            if (remoteEndPoint is IPEndPoint)
            {
                lock(toBeSendingQueue)
                {
                    if (isSending)
                    {
                        // TODO: 优化
                        toBeSendingQueue.Enqueue(new DatagramPacket() { Content = buff, EndPoint = remoteEndPoint as IPEndPoint });
                        return;
                    }

                    isSending = true;
                }

                SendMessageImpl(buff, remoteEndPoint as IPEndPoint);
            }
        }

        private void SendMessageImpl(byte[] buff, IPEndPoint endPoint)
        {
            sendSAEA.RemoteEndPoint = endPoint;
            sendSAEA.SetBuffer(buff, 0, buff.Length);

            SendToSocketEx(sendSAEA);
        }

        private void SendToSocketEx(SocketAsyncEventArgs e)
        {
            try
            {
                if (!socket.SendToAsync(e))
                {
                    OnSendCompleted(null, sendSAEA);
                }
            }
            catch (Exception err)
            {
                ShouldBeClose(err);
            }
        }

        private void OnSendCompleted(object sender, SocketAsyncEventArgs e)
        {
            if (state != ServerState.Start)
            {
                return;
            }

            if (e.SocketError != SocketError.Success)
            {
                ShouldBeClose(new Exception("发送错误！"));
                return;
            }

            if (e.Buffer.Length != e.BytesTransferred)
            {
                // 未完成发送
                e.SetBuffer(e.Buffer, e.Offset, e.Buffer.Length - e.BytesTransferred);
                Console.WriteLine("UDP包没有发送完！{0}/{1}", e.BytesTransferred, e.Buffer.Length);
                SendToSocketEx(e);
            }
            else
            {
                DatagramPacket nextPacket = null;
                lock(toBeSendingQueue)
                {
                    var cnt = toBeSendingQueue.Count;
                    if (cnt == 0)
                    {
                        isSending = false;
                        return;
                    }

                    nextPacket = toBeSendingQueue.Dequeue();
                }

                SendMessageImpl(nextPacket.Content, nextPacket.EndPoint);
            }
        }
        #endregion

        private void ShouldBeClose(Exception e)
        {
            Utils.logger.Error("捕捉到异常：{0}\nStackTrace: {1}", e.Message, e.StackTrace);
            Stop();
        }

        private Socket  socket;
        private ServerState state;
        private bool isSending;
        private SocketAsyncEventArgs sendSAEA;
        private Queue<DatagramPacket> toBeSendingQueue;

        private byte[] recvBuffer;
        private SocketAsyncEventArgs recvSAEA;

        sealed class DatagramPacket
        {
            public byte[] Content;
            public IPEndPoint EndPoint;
        }
    }
}
