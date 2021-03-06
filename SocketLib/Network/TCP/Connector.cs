﻿using System;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Threading;

namespace Base.Network
{
    // 建立socket连接
    // 通过回调函数返回socket
    public class Connector : IDisposable
    {
        public delegate void ConnectCallback(bool success, string errMsg, Socket s);

        public Connector()
        {
            state = None;

            connSAEA = new SocketAsyncEventArgs();
            connSAEA.Completed += OnConnectCompleted;
        }

        public void Close()
        {
            state = Closed;
            connSAEA.Completed -= OnConnectCompleted;
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
                ConnectAsync(address, port);
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

                // 调用回调
                callback.Invoke(true, "连接成功!", socket);
            }
            catch (Exception e)
            {
                Utils.logger.Error(string.Format("连接远程服务器出错！\nMessage: {0}\nStackTrace: {1}\n", e.Message, e.StackTrace), "Connector");
                callback.Invoke(false, e.Message, null);
            }
        }

        // 异步连接
        private void ConnectAsync(IPAddress address, int port)
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
                    OnConnectCompleted(this, connSAEA);
                }
            }
            catch (Exception e)
            {
                Utils.logger.Error(string.Format("连接[{0}/{1}]失败！\nMessage: {2}\nStackTrace: {1}", address, port, e.Message, e.StackTrace), "Connector");
                callback.Invoke(false, e.Message, null);
            }
        }

        // 异步连接回调
        private void OnConnectCompleted(object sender, SocketAsyncEventArgs e)
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

            // 调用回调
            callback.Invoke(true, "连接成功!", socket);
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
