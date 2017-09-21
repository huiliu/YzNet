﻿using System;
using System.Diagnostics;
using System.Net;

namespace Base.Network
{
    // UDP会话
    // 表示一个UDP客户端与UDP服务器的连接，使用KCP协议通讯，用conv来标识用户身份
    // TODO: 只用一个uint32数来识别客户端，可能不安全
    public class UdpSession : INetSession, IDisposable
    {

        public static UdpSession Create(uint conv, EndPoint clientEndPoint, UdpServer server)
        {
            var session = new UdpSession(conv, clientEndPoint, server);
            return session;
        }

        public UdpSession(uint conv, EndPoint clientEndPoint, UdpServer server)
        {
            this.conv      = conv;
            this.server    = server;
            SessionID      = conv;
            remoteEndPoint = clientEndPoint;
            IsConnected    = true;
            recvBuffer     = new ByteBuffer(NetworkCommon.UdpRecvBuffer);

            nextUpdateTimeMs = Utils.IClock();

            kcp = new KCP(conv, KcpOut);

            // TODO: 配置KCP参数
            lock(kcp)
            {
                kcp.NoDelay(1, 10, 2, 1);

                // 设置接收窗口/发送窗口
                kcp.WndSize(NetworkCommon.KcpSendWnd, NetworkCommon.KcpRecvWnd);
            }

        }

        public void Dispose()
        {
        }

        // 关闭UDP会话
        public override void Close()
        {
            IsConnected = false;
            base.Close();
        }

        // KCP更新
        public void Update()
        {
            if (IsConnected)
            {
                KcpUpdate(Utils.IClock());
            }
        }

        #region 发送数据
        // 发送网络消息
        public override void SendMessage(int msgID, ByteBuffer buffer)
        {
            lock(kcp)
            {
                // TODO: 需要调优
                if (kcp.WaitSnd() >= NetworkCommon.KcpSendWnd)
                {
                    // 累积太多KCP数据没有发送，也可以调高接收/发送窗口
                    Close();
                    return;
                }

                // 打包消息
                var data = MessageHeader.Encoding(msgID, buffer);

                // 将消息交给KCP
                int ret = kcp.Send(data.Buffer);
                Debug.Assert(ret == 0, "Send Data to KCP Failed!", "UDP");

                kcp.Flush();
            }
        }

        private void KcpOut(byte[] buff, int size)
        {
            Debug.Assert(server != null, "UdpServer不存在！", "UdpSession");

            try
            {
                if (!IsConnected)
                {
                    return;
                }

                // 将KCP中消息通过UDP Server发送给目标
                byte[] b = new byte[size];
                Array.Copy(buff, 0, b, 0, size);

                // 发送到网络
                server.SendMessage(b, remoteEndPoint);
            }
            catch (Exception e)
            {
                ShouldBeClose(e);
            }
        }
        #endregion

        #region 接收数据
        // "处理"收到的网络消息
        public void OnReceiveMessage(byte[] buff)
        {
            lock(kcp)
            {
                // 交给KCP处理
                var ret = kcp.Input(buff);
                Debug.Assert(ret == 0, "KCP INPUT数据出错！", "UDP");

                kcp.Flush();
            }

            CheckReceiveKcpMessage();
        }

        // 检查KCP是否有消息需要处理
        private void CheckReceiveKcpMessage()
        {
            lock(kcp)
            {
                // 读取KCP中的消息
                for (var sz = kcp.PeekSize(); sz > 0; sz = kcp.PeekSize())
                {
                    byte[] b = new byte[sz];
                    if (kcp.Recv(b) > 0)
                    {
                        // 处理收到的消息
                        ProcessReceivedMessage(b);
                    }
                }
            }
        }

        private void ProcessReceivedMessage(byte[] buff)
        {
            recvBuffer.WriteBytes(buff);

            int msgID = -1;
            byte[] msg = null;

            try
            {
                while ((msg = MessageHeader.TryDecode(recvBuffer, out msgID, out int cookie)) != null)
                {
                    // 将消息分发
                    triggerMessageReceived(this, msgID, msg);
                }
            }
            catch (Exception e)
            {
                Utils.logger.Error(string.Format("处理消息[{0}]出错！Message: {1}\nStackTrace: {2}", msgID, e.Message, e.StackTrace), ToString());
            }

            recvBuffer.TryDefragment();
        }
        #endregion

        // 检查异常原因，关闭会话
        private void ShouldBeClose(Exception e)
        {
            Console.WriteLine("捕捉到异常！Message: {0}\nStackTrace: {1}\n", e.Message, e.StackTrace);
            Close();
        }

        // 定时调用
        // TODO: 调用频繁，每个连接一个
        private void KcpUpdate(UInt32 currentMs)
        {

            if (currentMs >= nextUpdateTimeMs)
            {
                lock(kcp)
                {
                    kcp.Update(currentMs);
                    nextUpdateTimeMs = kcp.Check(currentMs);
                }

                CheckReceiveKcpMessage();
            }
        }

        public override void startReceive()
        {
        }

        private EndPoint remoteEndPoint;
        private UdpServer server;
        private ByteBuffer recvBuffer;

        #region KCP相关
        private uint conv;
        private KCP  kcp;
        private UInt32 nextUpdateTimeMs;
        #endregion
    }
}
