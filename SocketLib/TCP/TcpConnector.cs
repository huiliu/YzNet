using System;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Threading;

namespace YezhStudio.Base.Network
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
        public delegate void ConnectCallback(INetSession session, bool success, string errMsg);

        public TcpConnector()
        {
            state = None;

            connSAEA = new SocketAsyncEventArgs();
            connSAEA.Completed += onConnectCompleted;
        }

        public void Close()
        {
            state = Closed;
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

            IPAddress address;
            try
            {
                // 解析地址
                if (!IPAddress.TryParse(host, out address))
                {
                    // 解析地址失败，查询DNS
                    Dns.BeginGetHostEntry(host, getHostEntryCallback, callback);
                }
                else
                {
                    // 解析成功，开始连接
                    // connectAsync(address, port, callback);
                    connectSync(address, port, callback);
                }
            }
            catch (Exception e)
            {
                Debug.Write(string.Format("连接[{0}/{1}]失败！\nMessage: {2}\nStackTrace: {1}", host, port, e.Message, e.StackTrace), "Connector");
                callback.Invoke(null, false, e.Message);
            }
        }

        // 异步DNS解析回调
        private void getHostEntryCallback(IAsyncResult ar)
        {
            ConnectCallback callback = null;
            try
            {
                callback = (ConnectCallback)ar.AsyncState;
                var entries = Dns.EndGetHostEntry(ar);

                // 开始连接
                //connectAsync(entries.AddressList[0], port, callback);
                connectSync(entries.AddressList[0], port, callback);
            }
            catch (Exception e)
            {
                callback(null, false, e.Message);
                Close();
            }
        }

        private void connectSync(IPAddress address, int port, ConnectCallback cb)
        {
            try
            {
                socket = new Socket(address.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
                socket.Connect(new IPEndPoint(address, port));

                Debug.Assert(socket.Connected, "连接服务器失败！", "Connector");

                var session = new TcpSession(socket);
                // 开始接收数据
                session.startReceive();
                // 调用回调
                cb(session, true, "连接成功!");
            }
            catch (Exception e)
            {
                MainLog.Instance.Error(string.Format("连接远程服务器出错！\nMessage: {0}\nStackTrace: {1}\n", e.Message, e.StackTrace), "Connector");
            }
        }

        // 异步连接
        private void connectAsync(IPAddress address, int port, ConnectCallback callback)
        {
            Debug.Assert(state == Connecting);
            // TODO: IPV6
            socket = new Socket(address.AddressFamily, SocketType.Stream, ProtocolType.Tcp);

            connSAEA.UserToken      = new UserToken(callback, null);
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
                Debug.Write(string.Format("连接[{0}/{1}]失败！\nMessage: {2}\nStackTrace: {1}", address, port, e.Message, e.StackTrace), "Connector");
                callback.Invoke(null, false, e.Message);
            }
        }

        // 异步连接回调
        private void onConnectCompleted(object sender, SocketAsyncEventArgs e)
        {
            Debug.Assert(state == Connecting);

            var token = (UserToken)e.UserToken;

            if (e.SocketError != SocketError.Success)
            {
                token.callback(null, false, "连接失败！");
                Close();
                return;
            }

            if (Interlocked.CompareExchange(ref state, Connected, Connecting) != Connecting)
            {
                token.callback(null, false, "Connector状态不正确！");
                Close();
                return;
            }

            // 连接成功
            var session = new TcpSession(socket);
            // 开始接收数据
            session.startReceive();
            // 调用回调
            token.callback(session, true, "连接成功!");
        }

        private string ip;
        private int port;
        private Socket socket;

        private SocketAsyncEventArgs connSAEA;

        private int state;
        private readonly int None = 0;
        private readonly int Connecting = 1;
        private readonly int Connected  = 2;
        private readonly int Closed     = 3;

        internal class UserToken
        {
            public UserToken(ConnectCallback cb, Socket s)
            {
                callback = cb;
                socket = s;
            }
            public ConnectCallback callback;
            public Socket socket;
        }
    }
}
