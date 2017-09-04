﻿using System;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

using MessagePack;
using Server.Message;

namespace Server
{
    public class UdpSession : Session, IDisposable
    {
        public event Action<UdpSession, byte[]> OnMessageReceived;
        public UdpSession(uint conv, IPEndPoint clientEndPoint, UdpServer server)
        {
            this.remoteEndPoint = clientEndPoint;
            this.server = server;
            this.state = SessionState.Closed;

            this.needUpdateFlag   = false;
            this.nextUpdateTimeMs = Utils.IClock();

            kcp = new KCP(conv, async (buff, sz) =>
            {
                try
                {
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

        public void Start()
        {
            state = SessionState.Start;

            Task.Run(async () =>
            {
                while (state == SessionState.Start)
                {
                    update(Utils.IClock());
                    await Task.Delay(5);
                }
            });
        }

        public void Close()
        {
            state = SessionState.Closed;
        }

        private void shouldBeClose(Exception e)
        {
            Console.WriteLine("捕捉到异常！Message: {0}\nStackTrace: {1}\n", e.Message, e.StackTrace);
            Close();
        }

        public void SetMessageDispatcher(MessageDispatcher dispatcher)
        {
            this.dispatcher = dispatcher;
        }

        // 发送网络消息
        public async Task SendMessage(byte[] buffer)
        {
            // 将消息交给KCP
            int ret = kcp.Send(buffer);
            Debug.Assert(ret == 0, "Send Data to KCP Failed!", "UDP");
            needUpdateFlag = true;
        }

        public Task SendMessage(byte[] buffer, int offset, int count)
        {
            throw new NotImplementedException();
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

        internal void checkReceiveKcpMessage()
        {
            // 读取KCP中的消息
            for (var sz = kcp.PeekSize(); sz > 0; sz = kcp.PeekSize())
            {
                byte[] b = new byte[sz];
                if (kcp.Recv(b) > 0)
                {
                    // 将消息分发
                    // dispatcher.OnMessageReceived(this, b, 0, sz);
                    OnMessageReceived?.Invoke(this, b);
                }
            }
        }

        // 定时调用
        // TODO: 调用频繁，每个连接一个
        private void update(UInt32 currentMs)
        {
            checkReceiveKcpMessage();

            if (currentMs >= nextUpdateTimeMs ||
                needUpdateFlag)
            {
                kcp.Update(currentMs);

                nextUpdateTimeMs = kcp.Check(currentMs);
                needUpdateFlag   = false;
            }
        }

        public uint GetId()
        {
            return conv;
        }

        private MessageDispatcher dispatcher;
        private IPEndPoint remoteEndPoint;
        private UdpServer server;
        private SessionState state;

        #region KCP相关
        private uint conv;
        private KCP  kcp;
        private bool needUpdateFlag;
        private UInt32 nextUpdateTimeMs;

        #endregion
    }
}
