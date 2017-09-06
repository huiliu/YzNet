using System;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

using MessagePack;
using Server.Message;

namespace Server
{
    // UDP会话
    // 表示一个UDP客户端与UDP服务器的连接，使用KCP协议通讯，用conv来标识用户身份
    // TODO: 只用一个uint32数来识别客户端，可能不安全
    public class UdpSession : Session, IDisposable
    {
        public static UdpSession Create(uint conv, IPEndPoint clientEndPoint, UdpServer server)
        {
            var session = new UdpSession(conv, clientEndPoint, server);
            return session;
        }

        public UdpSession(uint conv, IPEndPoint clientEndPoint, UdpServer server)
        {
            this.conv = conv;
            this.remoteEndPoint = clientEndPoint;
            this.server = server;

            this.needUpdateFlag   = false;
            this.nextUpdateTimeMs = Utils.IClock();

            kcp = new KCP(conv, async (buff, sz) =>
            {
                try
                {
                    if (!IsConnected)
                    {
                        return;
                    }

                    // 将KCP中消息通过UDP Server发送给目标
                    byte[] b = new byte[sz];
                    Buffer.BlockCopy(buff, 0, b, 0, sz);
                    await server.SendMessage(b, remoteEndPoint);
                }
                catch (Exception e)
                {
                    shouldBeClose(e);
                }
            });

            // TODO: 配置KCP参数
            kcp.NoDelay(1, 10, 2, 1);
            kcp.WndSize(128, 128);
        }

        public void Dispose()
        {
        }

        // 关闭UDP会话
        public override void Close()
        {
            IsConnected = false;
            CanReceive = false;

            dispatcher.OnDisconnected(this);
        }

        public void Shutdown()
        {
            Close();
        }

        public override uint GetId()
        {
            return conv;
        }

        // 发送网络消息
        public override void SendMessage(byte[] buffer)
        {
            // 将消息交给KCP
            int ret = kcp.Send(buffer);
            Debug.Assert(ret == 0, "Send Data to KCP Failed!", "UDP");
            needUpdateFlag = true;
        }

        // "处理"收到的网络消息
        public void OnReceiveMessage(byte[] buff)
        {
            // 交给KCP处理
            var ret = kcp.Input(buff);
            Debug.Assert(ret == 0, "KCP INPUT数据出错！", "UDP");
            needUpdateFlag = true;

            checkReceiveKcpMessage();
        }

        // 检查异常原因，关闭会话
        private void shouldBeClose(Exception e)
        {
            Console.WriteLine("捕捉到异常！Message: {0}\nStackTrace: {1}\n", e.Message, e.StackTrace);
            Close();
        }

        // 检查KCP是否有消息需要处理
        private void checkReceiveKcpMessage()
        {
            // 读取KCP中的消息
            for (var sz = kcp.PeekSize(); sz > 0; sz = kcp.PeekSize())
            {
                byte[] b = new byte[sz];
                if (kcp.Recv(b) > 0)
                {
                    // 将消息分发
                    dispatcher.OnMessageReceived(this, b);
                }
            }
        }

        // 定时调用
        // TODO: 调用频繁，每个连接一个
        private void kcpUpdate(UInt32 currentMs)
        {

            if (currentMs >= nextUpdateTimeMs ||
                needUpdateFlag)
            {
                kcp.Update(currentMs);

                nextUpdateTimeMs = kcp.Check(currentMs);
                needUpdateFlag   = false;

                checkReceiveKcpMessage();
            }
        }

        // 启动KCP更新
        private void startUpdate()
        {
            Task.Run(async () =>
            {
                while (IsConnected)
                {
                    kcpUpdate(Utils.IClock());
                    await Task.Delay(5);
                }
            });
        }

        protected override void startReceive()
        {
            startUpdate();
        }

        private IPEndPoint remoteEndPoint;
        private UdpServer server;

        #region KCP相关
        private uint conv;
        private KCP  kcp;
        private bool needUpdateFlag;
        private UInt32 nextUpdateTimeMs;

        #endregion
    }
}
