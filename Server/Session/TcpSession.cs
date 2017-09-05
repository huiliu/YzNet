using System;
using System.Diagnostics;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace Server
{
    // TCP会话
    // 表示一条TCP连接
    public class TcpSession : Session
    {
        public TcpSession(uint id, TcpClient client)
        {
            this.id       = id;
            this.client   = client;

            stream        = client.GetStream();
        }

        // 启动会话
        // 开始接收网络消息
        public void Start()
        {
            Debug.Assert(stream.CanRead, "Socket没有连接！", "Session");

            client.Client.NoDelay = true;
            client.Client.SendBufferSize    = 1;
            client.Client.ReceiveBufferSize = 1;

            state  = SessionState.Start;

            // 在一个独立的线程开始接收消息
            Task.Run(async () =>
            {
                while(state != SessionState.Closed)
                    await startReceive();
            });
        }

        // 关闭Session
        public void Close()
        {
            state = SessionState.Closed;
            TcpSessionMgr.Instance.HandleSessionClosed(this);

            client.Close();
            dispatcher.OnDisconnected(this);
        }

        // 设置网络消息分发器
        public void SetMessageDispatcher(MessageDispatcher dispatcher)
        {
            this.dispatcher = dispatcher;
        }

        // 开始接收网络消息
        // TODO: 处理粘包问题，减少拷贝开销
        private async Task startReceive()
        {
            const int bufferSize = 1024;
            byte[] receiveBuffer = new byte[bufferSize];
            int receivedSize = 0;

            try
            {
                receivedSize = await stream.ReadAsync(receiveBuffer, 0, bufferSize);
                if (receivedSize == 0)
                {
                    Close();
                    return;
                }
            }
            catch (Exception e)
            {
                shouldBeClose(e);
                return;
            }

            // 消息应该放入主线程处理
            await dispatcher.OnMessageReceived(this, receiveBuffer, 0, receivedSize);
        }

        // 发送消息
        // 在主线程中调用
        public async Task SendMessage(byte[] buffer)
        {
            await SendMessage(buffer, 0, buffer.Length);
        }

        // 发送消息
        // 在主线程中调用
        public async Task SendMessage(byte[] buffer, int offset, int count)
        {
            await sendMessageImpl(buffer, offset, count);
        }

        // 非线程安全
        private async Task sendMessageImpl(byte[] buffer, int offset, int count)
        {
            // TODO: 线程安全问题
            try
            {
                await stream.WriteAsync(buffer, offset, count);
            }
            catch(Exception e)
            {
                shouldBeClose(e);
                return;
            }
        }

        public uint GetId()
        {
            return id;
        }

        private void shouldBeClose(Exception e)
        {
            Console.WriteLine("[Id: {2}]捕捉到异常!\nMessage: {0}\nStackTrace: {1}", e.Message, e.StackTrace, GetId());
            Close();
        }
        private uint                id;
        private TcpClient           client;
        private NetworkStream       stream;
        private SessionState        state;
        private MessageDispatcher   dispatcher;
    }
}
