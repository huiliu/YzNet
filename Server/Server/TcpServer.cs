using System;
using System.Diagnostics;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;

namespace Server
{
    // TCP服务器，侦听网络商品，接受新进入的连接
    // TODO:
    //  1. 连接数限制
    public class TcpServer
    {
        // 服务关闭
        public event Action             OnServerClose;

        // 处理新进入的连接
        // 在Accept线程中执行，如果有耗时操作应该放到其它线程
        public event Action<Socket>  OnNewConnection;

        public TcpServer()
        {
            port     = -1;
            listener = null;
        }

        public void StartServiceOn(ServerConfig cfg)
        {
            if (listener != null || port != -1)
            {
                Debug.Assert(false, "服务已经启动！");
                return;
            }

            try
            {
                // TODO: 其它相关配置
                listener = new TcpListener(IPAddress.Parse(cfg.IP), cfg.Port);
                listener.Server.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
                listener.Start();

                state = ServerState.Start;
            }
            catch (Exception e)
            {
                Debug.Write(string.Format("Server failed to start!\nMessage: {0}\nStackTrace: {1}", e.Message, e.StackTrace), "Server");
                return;
            }

            Console.WriteLine("服务启动成功！开始接受连接");

            // 在一个单独的线程中接收连接
            Task.Run(async () =>
            {
                await startAccept();
            });
        }

        public void Stop()
        {
            state = ServerState.Closed;
            listener.Stop();

            OnServerClose?.Invoke();
        }

        // 接受新来的网络连接
        private async Task startAccept()
        {
            Debug.Assert(state == ServerState.Start, "服务器没有启动！");

            while (state != ServerState.Closed)
            {
                try
                {
                    var client = await listener.AcceptSocketAsync();
                    handleNewConnection(client);
                }
                catch (Exception e)
                {
                    Debug.Write(string.Format("AcceptAsync throw exception!\nMessage: {0}\nStackTrace: {1}", e.Message, e.StackTrace), "Server");
                    state = ServerState.Closed;
                    Stop();
                }
            }
        }

        private void handleNewConnection(Socket socket)
        {
            OnNewConnection?.Invoke(socket);
        }

        public Task SendMessage(byte[] buff, object obj = null)
        {
            throw new NotImplementedException();
        }

        private int port;
        private TcpListener listener;
        private ServerState state;
    }
}
