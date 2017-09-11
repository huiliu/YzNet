using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;

namespace YezhStudio.Base.Network
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
        public event Action                                 OnServerClose;

        // 收到UDP数据
        // 需要处理可能是首次连接的客户端
        public event Action<UdpServer, EndPoint, byte[], int> OnMessageReceived;

        public UdpServer()
        {
            isSending = false;
            toBeSendingQueue = new Queue<DatagramPacket>();
            sendSAEA = new SocketAsyncEventArgs();
            sendSAEA.Completed += onSendCompleted;

            // TODO: 优化
            recvBuffer = new byte[NetworkCommon.UdpRecvBuffer];
            recvSAEA = new SocketAsyncEventArgs();
            recvSAEA.Completed += onRecvCompleted;
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
            startReceive();
        }

        // 停止服务
        public void Stop()
        {
            state = ServerState.Closed;

            recvSAEA.Completed -= onRecvCompleted;
            sendSAEA.Completed -= onSendCompleted;

            toBeSendingQueue.Clear();

            socket.Close();

            if (OnServerClose != null)
            {
                OnServerClose.Invoke();
            }
        }

        public void Dispose()
        {
            sendSAEA.Dispose();
            recvSAEA.Dispose();
        }

        private void startReceive()
        {
            recvSAEA.RemoteEndPoint = new IPEndPoint(IPAddress.Any, 0);
            recvSAEA.SetBuffer(recvBuffer, 0, recvBuffer.Length);

            try
            {
                if (!socket.ReceiveFromAsync(recvSAEA))
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
            if (state != ServerState.Start)
            {
                return;
            }

            if (e.BytesTransferred <= 0 || e.SocketError != SocketError.Success)
            {
                shouldBeClose(e.SocketError);
                return;
            }

            // 处理收到的网络消息，如果使用异步，需要将BUFFER拷贝一份
            OnMessageReceived(this, e.RemoteEndPoint, e.Buffer, e.BytesTransferred);

            // 继续收取
            startReceive();
        }
        // 发送消息至remoteEndPoint
        public void SendMessage(byte[] buff, object remoteEndPoint = null)
        {
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

                sendMessageImpl(buff, remoteEndPoint as IPEndPoint);
            }
        }

        private void sendMessageImpl(byte[] buff, IPEndPoint endPoint)
        {
            // TODO: 优化
            sendSAEA = new SocketAsyncEventArgs();
            sendSAEA.Completed += onSendCompleted;
            sendSAEA.RemoteEndPoint = endPoint;
            sendSAEA.SetBuffer(buff, 0, buff.Length);

            Console.WriteLine("remoteEndPoint: {0}", endPoint);
            sendToSocketEx(sendSAEA);
        }

        private void sendToSocketEx(SocketAsyncEventArgs e)
        {
            try
            {
                if (!socket.SendToAsync(e))
                {
                    onSendCompleted(null, sendSAEA);
                }
            }
            catch (Exception err)
            {
                shouldBeClose(err);
            }
        }

        private void onSendCompleted(object sender, SocketAsyncEventArgs e)
        {
            if (e.BytesTransferred > 0 && e.SocketError != SocketError.Success)
            {
                shouldBeClose(e.SocketError);
                return;
            }

            if (state != ServerState.Start)
            {
                return;
            }


            if (e.Buffer.Length != e.BytesTransferred)
            {
                // 未完成发送
                e.SetBuffer(e.Buffer, e.Offset, e.Buffer.Length - e.BytesTransferred);
                Console.WriteLine("UDP包没有发送完！{0}/{1}", e.BytesTransferred, e.Buffer.Length);
                sendToSocketEx(e);
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

                sendMessageImpl(nextPacket.Content, nextPacket.EndPoint);
            }
        }

        private void shouldBeClose(Exception e)
        {
            Console.WriteLine("捕捉到异常：{0}\nStackTrace: {1}", e.Message, e.StackTrace);
            Stop();
        }

        private void shouldBeClose(SocketError errCode)
        {
            Console.WriteLine("发生了错误！ErroCode: {0}", errCode);
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
