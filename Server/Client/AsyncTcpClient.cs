using System;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;

namespace Server
{
    public class ClientCfg
    {
        public string IP;
        public int Port; 
    }

    // 异步Tcp客户端，用于与服务端建立连接
    public class AsyncTcpClient : IDisposable
    {
        public event Action<AsyncTcpClient>                     OnConnected;        // 连接建立成功回调
        public event Action<AsyncTcpClient, byte[], int, int>   OnMessageReceived;  // 收到数据回调
        public event Action<AsyncTcpClient>                     OnDisconnected;     // 连接断开回调

        public AsyncTcpClient(ClientCfg cfg)
        {
            this.cfg = cfg;
        }

        public void Dispose()
        {
            stream.Dispose();
            client.Dispose();
        }

        // 连接服务器
        public async Task Connect()
        {
            try
            {
                await client.ConnectAsync(IPAddress.Parse(cfg.IP), cfg.Port);
                Debug.Assert(client.Client.Connected, string.Format("连接{0}/{1}失败！", cfg.IP, cfg.Port), "TcpClient");
                stream = client.GetStream();

                OnConnected?.Invoke(this);

                await Task.Run(async () =>
                {
                    while (true)
                    {
                        await receiveMessage();
                    }
                });
            }
            catch (Exception e)
            {
                shouldBeClose(e);
            }
        }

        // 关闭连接
        public void Close()
        {
            client.Close();
            OnDisconnected?.Invoke(this);
        }

        // 安全的关闭连接
        public void Shutdown()
        {
            // 关闭写
            client.Client.Shutdown(SocketShutdown.Send);
        }

        // 接收网络消息
        public async Task receiveMessage()
        {
            const int receiveBufferSize = 1024;
            try
            {
                byte[] buff = new byte[receiveBufferSize];
                int sz = await stream.ReadAsync(buff, 0, receiveBufferSize);

                if (sz == 0)
                {
                    Close();
                }

                // 调用回调处理收到的网络消息
                OnMessageReceived?.Invoke(this, buff, 0, sz);
            }
            catch (Exception e)
            {
                shouldBeClose(e); 
            }
        }

        // 发送消息
        public async Task SendMessage(byte[] buff)
        {
            await SendMessage(buff, 0, buff.Length);
        }

        // 发送消息
        public async Task SendMessage(byte[] buff, int offset, int count)
        {
            try
            {
                // TODO: 疑问,如果缓冲区满,此方法会保证发送完成吗?
                await stream.WriteAsync(buff, 0, count);
            }
            catch (Exception e)
            {
                shouldBeClose(e);
            }
        }

        private void shouldBeClose(Exception e)
        {
            Debug.WriteLine("发生错误!Message: {0}\nStackTrace: {1}", e.Message, e.StackTrace, "TcpClient");
            Close();
        }

        private ClientCfg       cfg;
        private TcpClient       client;
        private NetworkStream   stream;
    }
}
