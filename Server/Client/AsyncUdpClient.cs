using System;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;

namespace Server
{
    // 集成KCP功能的UDP客户端
    public class AsyncUdpClient : IDisposable
    {
        public event Action<AsyncUdpClient>                     OnConnected;        // 连接建立成功回调
        public event Action<AsyncUdpClient, byte[], int, int>   OnMessageReceived;  // 收到数据回调
        public event Action<AsyncUdpClient>                     OnDisconnected;     // 连接断开回调

        public AsyncUdpClient(ClientCfg cfg, UInt32 conv)
        {
            this.cfg = cfg;
            this.conv = conv;
            this.client = new UdpClient();
            this.kcp = new KCP(conv, async (buff, sz) =>
            {
                // 将KCP消息发送给服务器
                await client.SendAsync(buff, sz);
            });

            kcp.NoDelay(1, 10, 2, 1);
            kcp.WndSize(128, 128);


            Task.Run(() =>
            {
                while (true)
                {
                    kcpUpdate(Utils.IClock());
                    Task.Delay(10);
                }
            });
        }

        // 连接服务器
        public void Connect()
        {
            try
            {
                // 连接UDP服务器
                client.Connect(IPAddress.Parse(cfg.IP), cfg.Port);

                // 接收网络消息
                Task.Run(async () =>
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

        public void Dispose()
        {
            client.Dispose();
        }

        public void Close()
        {
            client.Close();
        }

        // 接收网络消息
        private async Task receiveMessage()
        {
            try
            {
                var result = await client.ReceiveAsync();
                OnReceiveMessage(result.Buffer);
            }
            catch (Exception e)
            {
                shouldBeClose(e);
            }
        }

        // "处理"收到的网络消息
        private void OnReceiveMessage(byte[] buff)
        {
            // 交给KCP处理
            var ret = kcp.Input(buff);
            Debug.Assert(ret == 0, "KCP INPUT数据出错！", "UDP");
            needUpdateFlag = true;

            // 读取KCP中的消息
            for (var sz = kcp.PeekSize(); sz > 0; sz = kcp.PeekSize())
            {
                byte[] b = new byte[sz];
                if (kcp.Recv(b) > 0)
                {
                    // 将消息分发
                    OnMessageReceived?.Invoke(this, b, 0, sz);
                }
            }
        }

        // 发送消息
        public void SendMessage(byte[] buff)
        {
            int ret = kcp.Send(buff);
            Debug.Assert(ret == 0, "Send Data into KCP Failed", this.ToString());
            needUpdateFlag = true;
        }

        public void SendMessage(byte[] buff, int offset, int count)
        {
            throw new NotImplementedException();
        }

        private void shouldBeClose(Exception e)
        {
            Debug.WriteLine(string.Format("发生错误！Message: {0}\nStackTrace: {1}", e.Message, e.StackTrace), "UdpCLient");
            Close();
        }

        // KCP定时Update
        private void kcpUpdate(UInt32 currentMs)
        {
            if (needUpdateFlag || nextUpdateTimeMs > Utils.IClock())
            {
                kcp.Update(currentMs);

                nextUpdateTimeMs = kcp.Check(currentMs);
                needUpdateFlag   = false;
            }
        }

        private ClientCfg cfg;
        private UdpClient client;

        private uint conv = 1;
        private KCP  kcp;
        private bool needUpdateFlag;
        private UInt32 nextUpdateTimeMs;
    }
}
