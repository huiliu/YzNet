using System;
using System.Diagnostics;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;

namespace Server
{
    // TODO:
    // 1. 连接数限制
    public class TcpServer : Server<TcpClient>
    {
        public event Action<TcpClient>  OnNewConnection;

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

            // 在一个单独的线程中接收连接
            Task.Factory.StartNew(async () =>
            {
                await startAccept();
            });
        }

        public void Stop()
        {
            state = ServerState.Closed;
            listener.Stop();
        }

        private async Task startAccept()
        {
            while (state != ServerState.Closed)
            {
                try
                {
                    // 如何关闭？
                    var client = await listener.AcceptTcpClientAsync();
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

        private void handleNewConnection(TcpClient client)
        {
            OnNewConnection?.Invoke(client);
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
