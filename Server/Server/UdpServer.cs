using System;
using System.Diagnostics;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Net;
using System.Net.Sockets;

namespace Server
{
    class UdpServer : Server<UdpClient>
    {
        public event Action OnErrorCallback;
        public event Action<UdpClient> OnNewConnection;

        public event Action<UdpReceiveResult, UdpServer> OnReceiveMessage;

        public UdpServer()
        {
            this.cfg   = null;
            this.state = ServerState.Closed;
        }

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

            lisener = new UdpClient(new IPEndPoint(IPAddress.Parse(cfg.IP), cfg.Port));
            state = ServerState.Start;

            // 开始收取网络数据
            Task.Run(async () =>
            {
                await startReceive();
            });
        }

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
                    Debug.Write(string.Format("UDP ReceiveAsync throw exception!\nMessage: {0}\nStackTrace: {1}", e.Message, e.StackTrace), "Server");
                    Stop();
                    return;
                }
            }
        }

        public void handleReceiveMessage(UdpReceiveResult result)
        {
            OnReceiveMessage?.Invoke(result, this);
        }

        public void Stop()
        {
            state = ServerState.Closed;
            lisener.Close();
        }

        public async Task SendMessage(byte[] buff, object obj = null)
        {
            if (obj is IPEndPoint)
            {
                int count = await lisener.SendAsync(buff, buff.Length, obj as IPEndPoint);
                Debug.Assert(count == buff.Length, "发送数据不完整！", "Server");
            }
        }

        private ServerState state;
        private ServerConfig cfg;
        private UdpClient   lisener;
    }
}
