using System;
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
            this.cfg   = null;
            this.state = ServerState.Closed;
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
        public async Task SendMessage(byte[] buff, object remoteEndPoint = null)
        {
            if (remoteEndPoint is IPEndPoint)
            {
                try
                {
                    int count = await lisener.SendAsync(buff, buff.Length, remoteEndPoint as IPEndPoint);
                    Debug.Assert(count == buff.Length, "发送数据不完整！", "Server");
                }
                catch (Exception e)
                {
                    shouldBeClose(e);
                }
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

        private ServerState     state;
        private ServerConfig    cfg;
        private UdpClient       lisener;
    }
}
