using System;
using System.Diagnostics;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace Server
{
    class TcpSession : Session
    {
        public TcpSession(uint id, TcpClient client)
        {
            this.id = id;
            this.client = client;
            this.stream = client.GetStream();
        }

        public void Start()
        {
            Debug.Assert(stream.CanRead, "Socket没有连接！", "Session");

            sendFlag = false;
            state  = SessionState.Start;

            // 开始接收消息
            Task.Run(async () =>
            {
                while(state != SessionState.Closed)
                    await startReceive();
            });
        }

        public void Close()
        {
            state = SessionState.Closed;
            TcpSessionMgr.Instance.HandleSessionClosed(this);

            client.Close();
            dispatcher.OnDisconnected(this);
        }

        public void SetMessageDispatcher(MessageDispatcher dispatcher)
        {
            this.dispatcher = dispatcher;
        }

        private async Task startReceive()
        {
            const int bufferSize = 1024;
            byte[] buffer = new byte[bufferSize];
            int count = 0;
            try
            {
                count = await stream.ReadAsync(buffer, 0, bufferSize);
            }
            catch (Exception e)
            {
                Debug.WriteLine("ReadAsync Failed!\nMessage: {0}\nStackTrace: {1}", e.Message, e.StackTrace);
                Close();
                return;
            }

            if (count == 0)
            {
                Close();
                return;
            }
            // 消息应该放入主线程处理
            await dispatcher.OnMessageReceived(this, buffer, 0, count);
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
                await stream.WriteAsync(buffer, offset, count).ConfigureAwait(false);
            }
            catch(Exception e)
            {
                Debug.WriteLine("ReadAsync Failed!\nMessage: {0}\nStackTrace: {1}", e.Message, e.StackTrace);
                Close();

                return;
            }

            // 检查发送队列
            if (sendBuffer.Available > 0)
            {

            }
        }

        public uint GetId()
        {
            return id;
        }

        private uint                id;
        private TcpClient           client;
        private NetworkStream       stream;
        private SessionState        state;
        private bool                sendFlag;
        private MessageDispatcher   dispatcher;
        private RingBuffer          sendBuffer;
    }
}
