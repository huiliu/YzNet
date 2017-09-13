using System;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Threading;

namespace Base.Network
{
    public class ClientCfg
    {
        public string IP;
        public int Port; 
    }

    // 用于与服务端建立连接
    // 通过回调函数取到会话对象
    public class TcpConnector : IDisposable
    {
        public delegate void ConnectCallback(bool success, string errMsg, INetSession session);

        public TcpConnector()
        {
            state = None;

            connSAEA = new SocketAsyncEventArgs();
            connSAEA.Completed += onConnectCompleted;
        }

        public void Close()
        {
            state = Closed;
            connSAEA.Completed -= onConnectCompleted;
        }

        public void Dispose()
        {
            connSAEA.Dispose();
        }

        // 以TCP协议连接服务器
        public void ConnectServer(string host, int port, ConnectCallback callback)
        {
            if (Interlocked.CompareExchange(ref state, Connecting, None) != None)
            {
                Console.Write("Connector当前状态不允许连接！[state: {0}]", state);
                return;
            }

            this.ip   = host;
            this.port = port;
            this.callback = callback;

            DNS.ResolveHost(host, (bool succ, string msg, IPAddress address) =>
            {
                if (!succ)
                {
                    callback(succ, msg, null);
                    return;
                }

                // 异步连接
                connectAsync(address, port);
            });
        }

        // 同步连接远程服务器
        private void connectSync(IPAddress address, int port)
        {
            try
            {
                var socket = new Socket(address.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
                socket.Connect(new IPEndPoint(address, port));

                Debug.Assert(socket.Connected, "连接服务器失败！", "Connector");

                var session = new TcpSession(socket);
                // 开始接收数据
                session.startReceive();
                // 调用回调
                callback.Invoke(true, "连接成功!", session);
            }
            catch (Exception e)
            {
                Utils.logger.Error(string.Format("连接远程服务器出错！\nMessage: {0}\nStackTrace: {1}\n", e.Message, e.StackTrace), "Connector");
                callback.Invoke(false, e.Message, null);
            }
        }

        // 异步连接
        private void connectAsync(IPAddress address, int port)
        {
            Debug.Assert(state == Connecting);
            // TODO: IPV6
            var socket = new Socket(address.AddressFamily, SocketType.Stream, ProtocolType.Tcp);

            connSAEA.UserToken      = socket;
            connSAEA.RemoteEndPoint = new IPEndPoint(address, port);

            try
            {
                if (!socket.ConnectAsync(connSAEA))
                {
                    onConnectCompleted(this, connSAEA);
                }
            }
            catch (Exception e)
            {
                Utils.logger.Error(string.Format("连接[{0}/{1}]失败！\nMessage: {2}\nStackTrace: {1}", address, port, e.Message, e.StackTrace), "Connector");
                callback.Invoke(false, e.Message, null);
            }
        }

        // 异步连接回调
        private void onConnectCompleted(object sender, SocketAsyncEventArgs e)
        {
            Debug.Assert(state == Connecting);

            var socket = (Socket)e.UserToken;

            if (e.SocketError != SocketError.Success)
            {
                callback.Invoke(false, "连接失败！", null);
                Close();
                return;
            }

            if (Interlocked.CompareExchange(ref state, Connected, Connecting) != Connecting)
            {
                callback.Invoke(false, "Connector状态不正确！", null);
                Close();
                return;
            }

            // 连接成功
            var session = new TcpSession(socket);
            // 开始接收数据
            session.startReceive();
            // 调用回调
            callback.Invoke(true, "连接成功!", session);
        }

        private string ip;
        private int port;
        public ConnectCallback callback;
        private SocketAsyncEventArgs connSAEA;

        private int state;

        private readonly int None = 0;
        private readonly int Connecting = 1;
        private readonly int Connected  = 2;
        private readonly int Closed     = 3;
    }
}
