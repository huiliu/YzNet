using System;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;

namespace YezhStudio.Base.Network
{
    // TCP服务器，侦听网络商品，接受新进入的连接
    // TODO:
    //  1. 连接数限制
    public class TcpServer : IDisposable
    {
        // 侦听服务关闭
        public event Action          OnServerClosed;

        // 处理新进入的连接
        public event Action<INetSession>  OnNewConnection;

        public TcpServer(string name)
        {
            this.name = name;
            socket    = null;
            isClosed  = false;

            acceptSAEA = new SocketAsyncEventArgs();
            acceptSAEA.Completed += onAcceptCompleted;
        }

        // 开始服务
        public void StartServiceOn(string ip, int port)
        {
            if (socket != null)
            {
                Debug.Assert(false, string.Format("[{0}]服务已经启动！", name));
                return;
            }

            Debug.Assert(OnNewConnection != null, string.Format("[{0}]没有设置处理新连接的回调! [{1}/{2}]", name, ip, port), ToString());

            try
            {
                // TODO: IPV6
                socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
                socket.Bind(new IPEndPoint(IPAddress.Parse(ip), port));
                socket.Listen (100);
                socket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);

                MainLog.Instance.Info("[{0}]服务启动成功！开始接受连接", name);

                startAcceptConnection();
            }
            catch (Exception e)
            {
                MainLog.Instance.Error(string.Format("Server[{0}]failed to start!\nMessage: {1}\nStackTrace: {2}", name, e.Message, e.StackTrace), "Server");
                return;
            }
        }

        // 停止服务
        public void Stop()
        {
            isClosed = true;
            acceptSAEA.Completed -= onAcceptCompleted;

            if (socket != null)
            {
                socket.Close();
            }

            if (OnServerClosed != null)
            {
                OnServerClosed.Invoke();
            }
        }

        public void Dispose()
        {
            acceptSAEA.Dispose();
        }

        // 接收新连接进入
        private void startAcceptConnection()
        {
            if (isClosed)
            {
                return;
            }

            try
            {
                acceptSAEA.AcceptSocket = null;
                // 开始接收连接
                if (!socket.AcceptAsync(acceptSAEA))
                {
                    onAcceptCompleted(this, acceptSAEA);
                }
            }
            catch (Exception e)
            {
                MainLog.Instance.Error(string.Format("Server[{0}]failed to start!\nMessage: {1}\nStackTrace: {2}", name, e.Message, e.StackTrace), "Server");
                isClosed = true;
                return;
            }
        }

        // 接收新连接回调
        private void onAcceptCompleted(object sender, SocketAsyncEventArgs e)
        {
            if (e.SocketError != SocketError.Success)
            {
                Stop();
                return;
            }

            Debug.Assert(e.AcceptSocket != null, string.Format("启动服务[{0}]失败！", name), ToString());

            try
            {
                if(OnNewConnection != null)
                {
                    OnNewConnection.Invoke(new TcpSession(e.AcceptSocket));
                }
            }
            catch (Exception err)
            {
                MainLog.Instance.Error(string.Format("[{0}]处理接收新连接失败!\nMessage: {1}\nStackTrace: {2}", name, err.Message, err.StackTrace), "Server");
            }
            finally
            {
                startAcceptConnection();
            }
        }

        private string name;
        private Socket socket;
        private bool isClosed;

        private SocketAsyncEventArgs acceptSAEA;
    }
}
