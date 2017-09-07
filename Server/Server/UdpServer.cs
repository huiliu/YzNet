using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Net;
using System.Net.Sockets;

namespace Server
{
    // UDP服务器，侦听特定端口，接受网络数据交给UdpSession
    public class UdpServer : IDisposable
    {
        // 服务关闭
        public event Action                                 OnServerClose;

        // 处理收到的UDP数据
        // 在UDP接收线程中执行，如果有耗时操作，应该放入到其它线程执行
        public event Action<UdpReceiveResult, UdpServer>    OnReceiveMessage;

        public UdpServer()
        {
            cfg = null;
            state = ServerState.Closed;
            isSending = false;
            toBeSendingQueue = new Queue<DatagramPacket>();
            sendSAEA = new SocketAsyncEventArgs();
            sendSAEA.Completed += SendCompleted;
        }

        public void Dispose()
        {
            lisener.Dispose();
        }

        // 启动UDP服务，开始接受"新连接"和数据
        public void StartServiceOn(ServerConfig cfg)
        {
            if (state != ServerState.Closed ||
                cfg == null ||
                lisener != null)
            {
                Debug.Assert(false, "Server已经启动!", "Server");
                return;
            }

            this.cfg = cfg;

            // 侦听Udp端口
            lisener = new UdpClient(new IPEndPoint(IPAddress.Parse(cfg.IP), cfg.Port));

            lisener.Client.Blocking = false;
            lisener.Client.SendBufferSize    = 1024 * 32;
            lisener.Client.ReceiveBufferSize = 1024 * 32;

            state = ServerState.Start;

            // 开始收取网络数据
            // 在一个独立线程中执行
            Task.Run(async () =>
            {
                await startReceive();
            });
        }

        // 停止服务
        public void Stop()
        {
            state = ServerState.Closed;
            lisener.Close();

            OnServerClose?.Invoke();
        }

        // 开始接收数据
        public async Task startReceive()
        {
            while (state == ServerState.Start)
            {
                try
                {
                    var receiveResult = await lisener.ReceiveAsync();
                    handleReceiveMessage(receiveResult);
                }
                catch (Exception e)
                {
                    shouldBeClose(e);
                    return;
                }
            }
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
            sendSAEA.UserToken = 0;
            sendSAEA.SocketError = SocketError.Success;
            sendSAEA.SocketFlags = SocketFlags.None;
            sendSAEA.RemoteEndPoint = endPoint;
            sendSAEA.SetBuffer(buff, 0, buff.Length);

            Console.WriteLine("remoteEndPoint: {0}", endPoint);
            sendToSocketEx(sendSAEA);
        }

        private void sendToSocketEx(SocketAsyncEventArgs e)
        {
            try
            {
                if (!lisener.Client.SendToAsync(e))
                {
                    SendCompleted(null, sendSAEA);
                }
            }
            catch (Exception err)
            {
                shouldBeClose(err);
            }
        }

        private void SendCompleted(object sender, SocketAsyncEventArgs e)
        {
            if (e.SocketError != SocketError.Success)
            {
                Console.WriteLine("ERROR remoteEndPoint: {0}", e.RemoteEndPoint);
                isSending = false;
                shouldBeClose(e.SocketError);
                return;
            }

            if (e.Buffer.Length != e.BytesTransferred)
            {
                // 未完成发送
                e.SetBuffer(e.Buffer, e.Offset, e.Buffer.Length - e.BytesTransferred);
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

        private void handleReceiveMessage(UdpReceiveResult result)
        {
            OnReceiveMessage?.Invoke(result, this);
        }

        private void shouldBeClose(Exception e)
        {
            Console.WriteLine("捕捉到异常：{0}\nStackTrace: {1}", e.Message, e.StackTrace);
            Stop();
        }

        private void shouldBeClose(SocketError errCode)
        {
            Console.WriteLine("发送了错误！ErroCode: {0}", errCode);
        }

        private ServerState     state;
        private ServerConfig    cfg;
        private UdpClient       lisener;
        private Queue<DatagramPacket> toBeSendingQueue;
        private bool isSending;
        private SocketAsyncEventArgs sendSAEA;

        sealed class DatagramPacket
        {
            public byte[] Content;
            public IPEndPoint EndPoint;
        }
    }
}
