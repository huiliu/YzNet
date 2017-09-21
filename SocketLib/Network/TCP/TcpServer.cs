using System;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;

namespace Base.Network
{
    // TCP服务器，侦听网络商品，接受新进入的连接
    public class TcpServer : IDisposable
    {
        // 侦听服务关闭
        public event Action                 OnServerClosed;

        // 处理新进入的连接
        public event Action<INetSession>    OnNewConnection;

        public TcpServer(string name)
        {
            this.name = name;
            socket    = null;
            isClosed  = false;

            acceptSAEA = new SocketAsyncEventArgs();
            acceptSAEA.Completed += OnAcceptCompleted;
        }

        // 开始服务
        public void StartServiceOn(string ip, int port)
        {
            if (socket != null)
            {
                Debug.Assert(false, string.Format("[{0}]服务已经启动！", name));
                return;
            }

            Debug.Assert(OnNewConnection != null, string.Format("[{0}]没有设置处理新连接的回调! [{1}/{2}]", name, ip, port), "TcpServer");

            try
            {
                socket = new Socket(AddressFamily.InterNetwork | AddressFamily.InterNetworkV6, SocketType.Stream, ProtocolType.Tcp);
                socket.Bind(new IPEndPoint(IPAddress.Parse(ip), port));
                socket.Listen (100);
                socket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);

                Utils.logger.Info("[{0}]服务启动成功！开始接受连接", name);

                StartAcceptConnection();
            }
            catch (Exception e)
            {
                Utils.logger.Error(string.Format("Server[{0}]failed to start!\nMessage: {1}\nStackTrace: {2}", name, e.Message, e.StackTrace), "Server");
                return;
            }
        }

        // 停止服务
        public void Stop()
        {
            isClosed = true;
            acceptSAEA.Completed -= OnAcceptCompleted;

            socket?.Close();
            OnServerClosed?.Invoke();
        }

        public void Dispose()
        {
            acceptSAEA.Dispose();
            socket.Dispose();
        }

        // 接收新连接进入
        private void StartAcceptConnection()
        {
            try
            {
                acceptSAEA.AcceptSocket = null;
                // 开始接收连接
                if (!socket.AcceptAsync(acceptSAEA))
                {
                    OnAcceptCompleted(this, acceptSAEA);
                }
            }
            catch (Exception e)
            {
                Utils.logger.Error(string.Format("Server[{0}]failed to start!\nMessage: {1}\nStackTrace: {2}", name, e.Message, e.StackTrace), "Server");
                isClosed = true;
                return;
            }
        }

        // 接收新连接回调
        private void OnAcceptCompleted(object sender, SocketAsyncEventArgs e)
        {
            if (isClosed)
            {
                Utils.logger.Info(string.Format("服务[{0}]已经关闭！", name), "TcpServer");
                return;
            }

            if (e.SocketError != SocketError.Success)
            {
                Stop();
                return;
            }

            Debug.Assert(e.AcceptSocket != null, string.Format("启动服务[{0}]失败！", name), "TcpServer");

            try
            {
                // 处理新连接
                OnNewConnection?.Invoke(new TcpSession(e.AcceptSocket));
            }
            catch (Exception err)
            {
                Utils.logger.Error(string.Format("[{0}]处理接收新连接失败!\nMessage: {1}\nStackTrace: {2}", name, err.Message, err.StackTrace), "TcpServer");
            }
            finally
            {
                // 继续侦听
                StartAcceptConnection();
            }
        }

        private string name;
        private Socket socket;
        private bool isClosed;

        private SocketAsyncEventArgs acceptSAEA;
    }
}
